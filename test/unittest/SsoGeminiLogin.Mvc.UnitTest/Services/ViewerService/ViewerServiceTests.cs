using SsoGeminiLogin.Mvc.Integrations.Interfaces;

namespace SsoGeminiLogin.Mvc.UnitTest.Services.ViewerService;

public sealed class ViewerServiceTests
{
	[Fact]
	public async Task StartAsyncDelegatesToIndependentBrowserBrokerApi()
	{
		StubBrowserBrokerClient broker = new();
		SsoGeminiLogin.Mvc.Services.ViewerService service = new(broker);

		BrowserSessionState result = await service.StartAsync("user@example.test");

		Assert.Equal("gbs_test", result.SessionId);
		Assert.Equal("user@example.test", broker.LastSsoUser);
	}

	private sealed class StubBrowserBrokerClient : IBrowserBrokerClient
	{
		private static readonly BrowserSessionState Session = new("gbs_test", "starting", null, null, null, null, [], false, false);
		public string? LastSsoUser { get; private set; }
		public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<BrowserAccount> GetAccountAsync(string ssoUser, CancellationToken cancellationToken = default) =>
			Task.FromResult(new BrowserAccount("account-1", "mapped@example.test"));
		public Task<BrowserSessionState> CreateSessionAsync(string ssoUser, CancellationToken cancellationToken = default)
		{
			LastSsoUser = ssoUser;
			return Task.FromResult(Session);
		}
		public Task<BrowserSessionState?> GetCurrentSessionAsync(string ssoUser, CancellationToken cancellationToken = default) => Task.FromResult<BrowserSessionState?>(null);
		public Task<BrowserSessionState?> GetSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default) => Task.FromResult<BrowserSessionState?>(Session);
		public Task<BrowserSessionState> OpenSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default) => Task.FromResult(Session);
		public Task EndSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}
}
