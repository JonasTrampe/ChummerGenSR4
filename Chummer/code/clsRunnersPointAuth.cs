using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Chummer
{
	/// <summary>
	/// OAuth2 Authorization Code + PKCE flow for the RunnersPoint API, plus token persistence. Chummer
	/// is distributed as a public client (no embeddable secret), so this uses PKCE with a loopback
	/// redirect and the system browser - the standard pattern for native/desktop OAuth clients.
	///
	/// Token storage: DPAPI-protected on Windows (CurrentUser scope, so only the OS account that logged
	/// in can decrypt it). On non-Windows platforms (Mono/Linux) there is currently no equivalent secret
	/// store wired up - tokens are written in plain text with a best-effort 0600 permission attempt, and
	/// GetAccessTokenAsync surfaces a warning the first time it has to do this. A real Linux secret store
	/// (e.g. libsecret) is tracked as follow-up work, not implemented here.
	/// </summary>
	public class RunnersPointAuth
	{
		// TODO: replace with Chummer's real registered public client id once RunnersPoint issues one.
		private const string ClientId = "chummer-desktop-TODO";
		private const string AuthorizationUrl = "https://accounts.runnerspoint.example/oauth/authorize";
		private const string TokenUrl = "https://accounts.runnerspoint.example/oauth/token";
		private const string Scopes = "documents:read documents:write";

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

		private class TokenSet
		{
			public string AccessToken { get; set; }
			public string RefreshToken { get; set; }
			public DateTime ExpiresAtUtc { get; set; }
		}

		private TokenSet _objCachedTokens;

		/// <summary>
		/// Whether a stored login already exists (does not guarantee it's still valid - the token may
		/// have been revoked server-side).
		/// </summary>
		public bool HasStoredLogin()
		{
			return File.Exists(TokenFilePath);
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
			if (File.Exists(TokenFilePath))
				File.Delete(TokenFilePath);
		}

		/// <summary>
		/// Returns a currently-valid access token, refreshing it first if it's expired. Throws if there's
		/// no stored login or the refresh token has itself been revoked - callers should catch this and
		/// prompt the user to LoginAsync() again.
		/// </summary>
		public async Task<string> GetAccessTokenAsync()
		{
			TokenSet objTokens = LoadTokens();
			if (objTokens == null)
				throw new InvalidOperationException("Not logged in to RunnersPoint.");

			if (DateTime.UtcNow < objTokens.ExpiresAtUtc)
				return objTokens.AccessToken;

			await RefreshTokensAsync(objTokens);
			return _objCachedTokens.AccessToken;
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

				JavaScriptSerializer objSerializer = new JavaScriptSerializer();
				Dictionary<string, object> objJson = objSerializer.Deserialize<Dictionary<string, object>>(strBody);

				TokenSet objTokens = new TokenSet();
				objTokens.AccessToken = objJson["access_token"].ToString();
				objTokens.RefreshToken = objJson.ContainsKey("refresh_token") ? objJson["refresh_token"].ToString() : (_objCachedTokens != null ? _objCachedTokens.RefreshToken : null);
				int intExpiresIn = objJson.ContainsKey("expires_in") ? Convert.ToInt32(objJson["expires_in"]) : 3600;
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

			if (!File.Exists(TokenFilePath))
				return null;

			byte[] bytStored = File.ReadAllBytes(TokenFilePath);
			byte[] bytJson;
			if (IsWindows())
			{
				bytJson = ProtectedData.Unprotect(bytStored, null, DataProtectionScope.CurrentUser);
			}
			else
			{
				bytJson = bytStored;
			}

			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			Dictionary<string, object> objJson = objSerializer.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(bytJson));

			TokenSet objTokens = new TokenSet();
			objTokens.AccessToken = objJson["accessToken"].ToString();
			objTokens.RefreshToken = objJson.ContainsKey("refreshToken") ? objJson["refreshToken"].ToString() : null;
			objTokens.ExpiresAtUtc = DateTime.Parse(objJson["expiresAtUtc"].ToString()).ToUniversalTime();

			_objCachedTokens = objTokens;
			return objTokens;
		}

		private void SaveTokens(TokenSet objTokens)
		{
			JavaScriptSerializer objSerializer = new JavaScriptSerializer();
			string strJson = objSerializer.Serialize(new Dictionary<string, object>
			{
				{ "accessToken", objTokens.AccessToken },
				{ "refreshToken", objTokens.RefreshToken },
				{ "expiresAtUtc", objTokens.ExpiresAtUtc.ToString("o") },
			});
			byte[] bytJson = Encoding.UTF8.GetBytes(strJson);

			byte[] bytToWrite;
			if (IsWindows())
			{
				bytToWrite = ProtectedData.Protect(bytJson, null, DataProtectionScope.CurrentUser);
			}
			else
			{
				// No cross-platform secret store wired up yet - see class remarks. Flagged loudly rather
				// than silently pretending this is as safe as the Windows path.
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
