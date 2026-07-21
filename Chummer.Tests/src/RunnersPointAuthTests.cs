using System;
using System.Threading.Tasks;
using Chummer.Core;
using Xunit;

namespace Chummer.Tests
{
	// These tests exercise SetApiToken/GetAccessTokenAsync/Logout, which persist to a fixed file under
	// %AppData%\ChummerGenSR4\cloudauth.dat. Each test cleans up via Logout() in a finally block so it
	// doesn't leave a real stored login behind on the machine running the tests.
	public class RunnersPointAuthTests
	{
		private const string ValidToken = "rp_0123456789abcdef0123456789abcdef01234567";

		[Fact]
		public async Task SetApiToken_ThenGetAccessToken_ReturnsTheSameToken()
		{
			var objAuth = new RunnersPointAuth();
			try
			{
				objAuth.SetApiToken(ValidToken);

				var strToken = await objAuth.GetAccessTokenAsync();

				Assert.Equal(ValidToken, strToken);
				Assert.True(objAuth.HasStoredLogin());
			}
			finally
			{
				objAuth.Logout();
			}
		}

		[Fact]
		public void SetApiToken_TrimsWhitespace()
		{
			var objAuth = new RunnersPointAuth();
			try
			{
				objAuth.SetApiToken("  " + ValidToken + "  ");

				Assert.True(objAuth.HasStoredLogin());
			}
			finally
			{
				objAuth.Logout();
			}
		}

		[Theory]
		[InlineData("")]
		[InlineData("not-a-token")]
		[InlineData("rp_tooshort")]
		public void SetApiToken_RejectsMalformedTokens(string strBadToken)
		{
			var objAuth = new RunnersPointAuth();

			Assert.Throws<ArgumentException>(() => objAuth.SetApiToken(strBadToken));
			Assert.False(objAuth.HasStoredLogin());
		}

		[Fact]
		public async Task Logout_ClearsStoredLogin()
		{
			var objAuth = new RunnersPointAuth();
			objAuth.SetApiToken(ValidToken);
			Assert.True(objAuth.HasStoredLogin());

			objAuth.Logout();

			Assert.False(objAuth.HasStoredLogin());
			await Assert.ThrowsAsync<InvalidOperationException>(() => objAuth.GetAccessTokenAsync());
		}

		[Fact]
		public async Task ApiToken_SurvivesReloadFromDisk()
		{
			// Regression test: SaveTokens serializes a null RefreshToken (always the case for an
			// apiToken login) as a JSON null, which is still a *present* key - LoadTokens' ContainsKey
			// check alone doesn't catch that, and calling .ToString() on the null value threw a
			// NullReferenceException. That only happened once _objCachedTokens was empty and it had to
			// actually deserialize the file, which a single RunnersPointAuth instance's own in-memory
			// cache masked - so this simulates a fresh process by using a second instance.
			var objAuth = new RunnersPointAuth();
			try
			{
				objAuth.SetApiToken(ValidToken);

				var objReloaded = new RunnersPointAuth();
				var strToken = await objReloaded.GetAccessTokenAsync();

				Assert.Equal(ValidToken, strToken);
			}
			finally
			{
				objAuth.Logout();
			}
		}

		[Fact]
		public async Task TryForceRefreshAsync_ReturnsFalseForApiTokenLogin()
		{
			// apiToken logins have no refresh token at all - there's nothing to refresh, so a 401 retry
			// should fall straight through to the normal auth-expired handling instead of attempting (and
			// failing) an OAuth-style refresh.
			var objAuth = new RunnersPointAuth();
			try
			{
				objAuth.SetApiToken(ValidToken);

				var blnRefreshed = await objAuth.TryForceRefreshAsync();

				Assert.False(blnRefreshed);
			}
			finally
			{
				objAuth.Logout();
			}
		}

		[Fact]
		public async Task TryForceRefreshAsync_ReturnsFalseWhenNotLoggedIn()
		{
			var objAuth = new RunnersPointAuth();

			var blnRefreshed = await objAuth.TryForceRefreshAsync();

			Assert.False(blnRefreshed);
		}

		[Fact]
		public async Task SetApiToken_ReplacesAPreviousLogin()
		{
			var objAuth = new RunnersPointAuth();
			try
			{
				objAuth.SetApiToken(ValidToken);
				var strSecondToken = "rp_fedcba9876543210fedcba9876543210fedcba98";
				objAuth.SetApiToken(strSecondToken);

				var strToken = await objAuth.GetAccessTokenAsync();

				Assert.Equal(strSecondToken, strToken);
			}
			finally
			{
				objAuth.Logout();
			}
		}
	}
}
