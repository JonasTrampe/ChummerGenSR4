using System;
using System.Threading.Tasks;
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
			RunnersPointAuth objAuth = new RunnersPointAuth();
			try
			{
				objAuth.SetApiToken(ValidToken);

				string strToken = await objAuth.GetAccessTokenAsync();

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
			RunnersPointAuth objAuth = new RunnersPointAuth();
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
			RunnersPointAuth objAuth = new RunnersPointAuth();

			Assert.Throws<ArgumentException>(() => objAuth.SetApiToken(strBadToken));
			Assert.False(objAuth.HasStoredLogin());
		}

		[Fact]
		public async Task Logout_ClearsStoredLogin()
		{
			RunnersPointAuth objAuth = new RunnersPointAuth();
			objAuth.SetApiToken(ValidToken);
			Assert.True(objAuth.HasStoredLogin());

			objAuth.Logout();

			Assert.False(objAuth.HasStoredLogin());
			await Assert.ThrowsAsync<InvalidOperationException>(() => objAuth.GetAccessTokenAsync());
		}

		[Fact]
		public async Task SetApiToken_ReplacesAPreviousLogin()
		{
			RunnersPointAuth objAuth = new RunnersPointAuth();
			try
			{
				objAuth.SetApiToken(ValidToken);
				string strSecondToken = "rp_fedcba9876543210fedcba9876543210fedcba98";
				objAuth.SetApiToken(strSecondToken);

				string strToken = await objAuth.GetAccessTokenAsync();

				Assert.Equal(strSecondToken, strToken);
			}
			finally
			{
				objAuth.Logout();
			}
		}
	}
}
