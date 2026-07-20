using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Chummer
{
	/// <summary>
	/// Authentication against the RunnersPoint API. Supports two mechanisms:
	///
	/// - apiToken: a long-lived bearer token (`rp_...`, minted server-side via `POST /api/v1/tokens`
	///   while logged into the RunnersPoint website, up to a 90-day expiry) pasted into Chummer via
	///   SetApiToken. This is what the real RunnersPoint server actually implements today - its
	///   security firewall only wires up a custom ApiTokenAuthenticator, not an OAuth2 authorization
	///   server - so this is the path that works right now.
	/// - OAuth2 Authorization Code + PKCE via LoginAsync/the system browser: kept for if/when
	///   RunnersPoint stands up a real authorization server. Chummer is a public client (no embeddable
	///   secret), so PKCE with a loopback redirect is the standard pattern for that case. Untested
	///   against a live server since none exists yet.
	///
	/// Token storage: DPAPI-protected on Windows (CurrentUser scope, so only the OS account that logged
	/// in can decrypt it). On non-Windows platforms (Mono/Linux), libsecret is used through its standard
	/// secret-tool CLI. If libsecret is not
	/// available, tokens are written in plain text with a best-effort 0600 permission attempt.
	/// </summary>
	public class RunnersPointAuth : IRunnersPointAuth
	{
		// TODO: replace with Chummer's real registered public client id once RunnersPoint issues one.
		private const string ClientId = "chummer-desktop-TODO";
		private const string AuthorizationUrl = "https://accounts.runnerspoint.example/oauth/authorize";
		private const string TokenUrl = "https://accounts.runnerspoint.example/oauth/token";
		private const string Scopes = "documents:read documents:write shared_documents:read shared_documents:write";
		private const string SecretToolName = "secret-tool";
		private const string SecretAttributeArguments = "application ChummerGenSR4 service RunnersPointAuth";

		private static string TokenFilePath
		{
			get
			{
				string strDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChummerGenSR4");
				if (!Directory.Exists(strDir))
					Directory.CreateDirectory(strDir);
				return Path.Combine(strDir, "cloudauth.dat");
			}
		}

		[DataContract]
		private class TokenSet
		{
			[DataMember(Name = "accessToken")]
			public string AccessToken { get; set; }
			[DataMember(Name = "refreshToken")]
			public string RefreshToken { get; set; }
			[DataMember(Name = "expiresAtUtc")]
			public DateTime ExpiresAtUtc { get; set; }
			// True for a pasted apiToken (SetApiToken) - these have no refresh token and are used as-is
			// until the server itself rejects them (expired or revoked), rather than tracked against a
			// client-side expiry.
			[DataMember(Name = "isApiToken")]
			public bool IsApiToken { get; set; }
		}

		[DataContract]
		private class TokenResponse
		{
			[DataMember(Name = "access_token")]
			public string AccessToken { get; set; }
			[DataMember(Name = "refresh_token")]
			public string RefreshToken { get; set; }
			[DataMember(Name = "expires_in")]
			public int ExpiresIn { get; set; }
		}

		private TokenSet _objCachedTokens;

		/// <summary>
		/// Whether a stored login already exists (does not guarantee it's still valid - the token may
		/// have been revoked server-side).
		/// </summary>
		public bool HasStoredLogin()
		{
			if (!IsWindows() && TryLoadSecret(out _))
				return true;
			return File.Exists(TokenFilePath);
		}

		/// <summary>
		/// Whether the current stored login (if any) is a pasted apiToken rather than an OAuth login.
		/// Meaningless if HasStoredLogin() is false.
		/// </summary>
		public bool IsApiTokenLogin()
		{
			TokenSet objTokens = LoadTokens();
			return objTokens != null && objTokens.IsApiToken;
		}

		/// <summary>
		/// Runs the full interactive login flow: opens the system browser, listens on a loopback port
		/// for the redirect, exchanges the code for tokens, and persists them.
		/// </summary>
		public async Task LoginAsync()
		{
			string strCodeVerifier = GenerateCodeVerifier();
			string strCodeChallenge = GenerateCodeChallenge(strCodeVerifier);
			string strState = Guid.NewGuid().ToString("N");

			using (HttpListener objListener = new HttpListener())
			{
				int intPort = GetAvailableLoopbackPort();
				string strRedirectUri = "http://127.0.0.1:" + intPort + "/callback/";
				objListener.Prefixes.Add(strRedirectUri);
				objListener.Start();

				string strAuthorizeUrl = AuthorizationUrl
					+ "?response_type=code"
					+ "&client_id=" + Uri.EscapeDataString(ClientId)
					+ "&redirect_uri=" + Uri.EscapeDataString(strRedirectUri)
					+ "&scope=" + Uri.EscapeDataString(Scopes)
					+ "&state=" + strState
					+ "&code_challenge=" + strCodeChallenge
					+ "&code_challenge_method=S256";

				Process.Start(new ProcessStartInfo(strAuthorizeUrl) { UseShellExecute = true });

				HttpListenerContext objContext = await objListener.GetContextAsync();
				string strCode = objContext.Request.QueryString["code"];
				string strReturnedState = objContext.Request.QueryString["state"];
				string strError = objContext.Request.QueryString["error"];

				string strResponseHtml = string.IsNullOrEmpty(strError) && strReturnedState == strState
					? "<html><body>Login complete - you can close this tab and return to Chummer.</body></html>"
					: "<html><body>Login failed - please return to Chummer and try again.</body></html>";
				byte[] bytResponse = Encoding.UTF8.GetBytes(strResponseHtml);
				objContext.Response.ContentLength64 = bytResponse.Length;
				objContext.Response.OutputStream.Write(bytResponse, 0, bytResponse.Length);
				objContext.Response.OutputStream.Close();
				objListener.Stop();

				if (!string.IsNullOrEmpty(strError))
					throw new InvalidOperationException("RunnersPoint login failed: " + strError);
				if (strReturnedState != strState)
					throw new InvalidOperationException("RunnersPoint login failed: state mismatch (possible CSRF).");

				await ExchangeCodeForTokensAsync(strCode, strCodeVerifier, strRedirectUri);
			}
		}

		public void Logout()
		{
			_objCachedTokens = null;
			if (!IsWindows())
				ClearSecret();
			if (File.Exists(TokenFilePath))
				File.Delete(TokenFilePath);
		}

		/// <summary>
		/// Stores a pre-minted RunnersPoint apiToken (format "rp_...", from POST /api/v1/tokens on the
		/// website) as the credential to use, replacing any existing OAuth or apiToken login. There is no
		/// refresh for this path - once the server rejects it (expired or revoked), the caller needs a
		/// fresh token pasted in again.
		/// </summary>
		public void SetApiToken(string strToken)
		{
			strToken = (strToken ?? "").Trim();
			if (!strToken.StartsWith("rp_", StringComparison.Ordinal) || strToken.Length < 35)
				throw new ArgumentException("That doesn't look like a RunnersPoint API token - it should start with \"rp_\".");

			TokenSet objTokens = new TokenSet
			{
				AccessToken = strToken,
				RefreshToken = null,
				IsApiToken = true,
				ExpiresAtUtc = DateTime.MaxValue,
			};
			SaveTokens(objTokens);
			_objCachedTokens = objTokens;
		}

		/// <summary>
		/// Returns a currently-valid access token. For a pasted apiToken, that's just the stored token -
		/// there's no client-tracked expiry or refresh for it. For an OAuth login, refreshes first if
		/// expired. Throws if there's no stored login or the refresh token has itself been revoked -
		/// callers should catch this and prompt the user to log in again.
		/// </summary>
		public async Task<string> GetAccessTokenAsync()
		{
			TokenSet objTokens = LoadTokens();
			if (objTokens == null)
				throw new InvalidOperationException("Not logged in to RunnersPoint.");

			if (objTokens.IsApiToken || DateTime.UtcNow < objTokens.ExpiresAtUtc)
				return objTokens.AccessToken;

			await RefreshTokensAsync(objTokens);
			return _objCachedTokens.AccessToken;
		}

		/// <summary>
		/// Forces a token refresh regardless of the locally-tracked expiry, for retrying a request that
		/// the server itself just rejected with 401 (the local expiry clock and the server's may have
		/// drifted, or the access token could have been revoked independently of its stated lifetime).
		/// Returns false without throwing for anything that can't be refreshed this way - a pasted
		/// apiToken (no refresh token at all) or a refresh attempt that itself fails - so callers can
		/// fall back to treating the login as dead.
		/// </summary>
		public async Task<bool> TryForceRefreshAsync()
		{
			TokenSet objTokens = LoadTokens();
			if (objTokens == null || objTokens.IsApiToken || string.IsNullOrEmpty(objTokens.RefreshToken))
				return false;

			try
			{
				await RefreshTokensAsync(objTokens);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private async Task ExchangeCodeForTokensAsync(string strCode, string strCodeVerifier, string strRedirectUri)
		{
			Dictionary<string, string> objFormData = new Dictionary<string, string>
			{
				{ "grant_type", "authorization_code" },
				{ "code", strCode },
				{ "redirect_uri", strRedirectUri },
				{ "client_id", ClientId },
				{ "code_verifier", strCodeVerifier },
			};
			await RequestAndStoreTokensAsync(objFormData);
		}

		private async Task RefreshTokensAsync(TokenSet objExpiredTokens)
		{
			if (string.IsNullOrEmpty(objExpiredTokens.RefreshToken))
				throw new InvalidOperationException("RunnersPoint session expired and cannot be refreshed - please log in again.");

			Dictionary<string, string> objFormData = new Dictionary<string, string>
			{
				{ "grant_type", "refresh_token" },
				{ "refresh_token", objExpiredTokens.RefreshToken },
				{ "client_id", ClientId },
			};
			await RequestAndStoreTokensAsync(objFormData);
		}

		private async Task RequestAndStoreTokensAsync(Dictionary<string, string> objFormData)
		{
			using (HttpClient objHttpClient = new HttpClient())
			using (FormUrlEncodedContent objContent = new FormUrlEncodedContent(objFormData))
			{
				HttpResponseMessage objResponse = await objHttpClient.PostAsync(TokenUrl, objContent);
				string strBody = await objResponse.Content.ReadAsStringAsync();
				if (!objResponse.IsSuccessStatusCode)
					throw new InvalidOperationException("RunnersPoint token request failed (" + objResponse.StatusCode + "): " + strBody);

				TokenResponse objJson = Deserialize<TokenResponse>(strBody);

				TokenSet objTokens = new TokenSet();
				objTokens.AccessToken = objJson.AccessToken;
				objTokens.RefreshToken = !string.IsNullOrEmpty(objJson.RefreshToken) ? objJson.RefreshToken : (_objCachedTokens != null ? _objCachedTokens.RefreshToken : null);
				int intExpiresIn = objJson.ExpiresIn > 0 ? objJson.ExpiresIn : 3600;
				// Refresh a little early so a request doesn't race an expiry that happens mid-flight.
				objTokens.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(intExpiresIn - 60);

				SaveTokens(objTokens);
				_objCachedTokens = objTokens;
			}
		}

		private TokenSet LoadTokens()
		{
			if (_objCachedTokens != null)
				return _objCachedTokens;

			byte[] bytStored = null;
			if (!IsWindows() && TryLoadSecret(out string strSecret))
				bytStored = Encoding.UTF8.GetBytes(strSecret);
			else if (File.Exists(TokenFilePath))
				bytStored = File.ReadAllBytes(TokenFilePath);
			else
				return null;
			byte[] bytJson;
			if (IsWindows())
			{
				bytJson = ProtectedData.Unprotect(bytStored, null, DataProtectionScope.CurrentUser);
			}
			else
			{
				bytJson = bytStored;
			}

			TokenSet objTokens = Deserialize<TokenSet>(Encoding.UTF8.GetString(bytJson));

			objTokens.ExpiresAtUtc = objTokens.ExpiresAtUtc.ToUniversalTime();

			_objCachedTokens = objTokens;
			return objTokens;
		}

		private void SaveTokens(TokenSet objTokens)
		{
			string strJson = Serialize(objTokens);
			byte[] bytJson = Encoding.UTF8.GetBytes(strJson);

			byte[] bytToWrite;
			if (IsWindows())
			{
				bytToWrite = ProtectedData.Protect(bytJson, null, DataProtectionScope.CurrentUser);
			}
			else if (TryStoreSecret(strJson))
			{
				if (File.Exists(TokenFilePath))
					File.Delete(TokenFilePath);
				return;
			}
			else
			{
				// libsecret is unavailable. Flag this loudly rather than silently pretending this is
				// as safe as the Windows or libsecret-backed path.
				Trace.TraceWarning("RunnersPoint auth tokens are being stored in plain text on this platform - no secret store integration exists for Mono/Linux yet.");
				bytToWrite = bytJson;
			}

			File.WriteAllBytes(TokenFilePath, bytToWrite);
			if (!IsWindows())
			{
				try
				{
					// Best-effort lockdown; not a substitute for real secret storage.
					Process.Start(new ProcessStartInfo("chmod", "600 \"" + TokenFilePath + "\"") { UseShellExecute = false, CreateNoWindow = true })?.WaitForExit();
				}
				catch
				{
				}
			}
		}

		private static T Deserialize<T>(string strJson)
		{
			using (MemoryStream objStream = new MemoryStream(Encoding.UTF8.GetBytes(strJson)))
				return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(objStream);
		}

		private static string Serialize<T>(T objValue)
		{
			using (MemoryStream objStream = new MemoryStream())
			{
				new DataContractJsonSerializer(typeof(T)).WriteObject(objStream, objValue);
				return Encoding.UTF8.GetString(objStream.ToArray());
			}
		}

		private static bool TryLoadSecret(out string strSecret)
		{
			strSecret = null;
			try
			{
				ProcessStartInfo objStartInfo = new ProcessStartInfo(SecretToolName, "lookup " + SecretAttributeArguments)
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
				};
				using (Process objProcess = Process.Start(objStartInfo))
				{
					strSecret = objProcess.StandardOutput.ReadToEnd().TrimEnd('\r', '\n');
					objProcess.WaitForExit();
					return objProcess.ExitCode == 0 && !string.IsNullOrEmpty(strSecret);
				}
			}
			catch
			{
				return false;
			}
		}

		private static bool TryStoreSecret(string strSecret)
		{
			try
			{
				ProcessStartInfo objStartInfo = new ProcessStartInfo(SecretToolName, "store --label=Chummer RunnersPoint credentials " + SecretAttributeArguments)
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardInput = true,
				};
				using (Process objProcess = Process.Start(objStartInfo))
				{
					objProcess.StandardInput.Write(strSecret);
					objProcess.StandardInput.Close();
					objProcess.WaitForExit();
					return objProcess.ExitCode == 0;
				}
			}
			catch
			{
				return false;
			}
		}

		private static void ClearSecret()
		{
			try
			{
				using (Process objProcess = Process.Start(new ProcessStartInfo(SecretToolName, "clear " + SecretAttributeArguments) { UseShellExecute = false, CreateNoWindow = true }))
					objProcess.WaitForExit();
			}
			catch
			{
			}
		}

		private static bool IsWindows()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
		}

		private static string GenerateCodeVerifier()
		{
			byte[] bytRandom = new byte[32];
			using (RandomNumberGenerator objRng = RandomNumberGenerator.Create())
				objRng.GetBytes(bytRandom);
			return Base64UrlEncode(bytRandom);
		}

		private static string GenerateCodeChallenge(string strCodeVerifier)
		{
			using (SHA256 objSha256 = SHA256.Create())
			{
				byte[] bytHash = objSha256.ComputeHash(Encoding.ASCII.GetBytes(strCodeVerifier));
				return Base64UrlEncode(bytHash);
			}
		}

		private static string Base64UrlEncode(byte[] bytInput)
		{
			return Convert.ToBase64String(bytInput).Replace('+', '-').Replace('/', '_').TrimEnd('=');
		}

		private static int GetAvailableLoopbackPort()
		{
			System.Net.Sockets.TcpListener objSocketListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
			objSocketListener.Start();
			int intPort = ((IPEndPoint)objSocketListener.LocalEndpoint).Port;
			objSocketListener.Stop();
			return intPort;
		}
	}
}
