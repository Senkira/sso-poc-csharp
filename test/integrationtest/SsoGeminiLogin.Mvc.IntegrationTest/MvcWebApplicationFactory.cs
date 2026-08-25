using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SsoGeminiLogin.Mvc.Integrations.Interfaces;

namespace SsoGeminiLogin.Mvc.IntegrationTest;

public sealed class MvcWebApplicationFactory : WebApplicationFactory<Program>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Development");
		builder.ConfigureAppConfiguration((_, configuration) =>
		{
			configuration.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["BrokerApi:GatewayKey"] = "integration-test-gateway-key-1234567890",
				["DevSso:Enabled"] = "true",
				["DevSso:Username"] = "ssotest01",
				["DevSso:Password"] = "123456",
				["DevSso:MappedUser"] = "codeassist.04@easybuy.co.th"
			});
		});
		builder.ConfigureServices(services =>
		{
			services.RemoveAll<IBrowserBrokerClient>();
			services.AddSingleton<IBrowserBrokerClient, StubBrowserBrokerClient>();
		});
	}

	private sealed class StubBrowserBrokerClient : IBrowserBrokerClient
	{
		private static readonly BrowserSessionState ReadySession = new(
			"gbs_test-session", "ready", "Gemini พร้อมใช้งาน", "codeassist.04@easybuy.co.th",
			DateTimeOffset.UtcNow, new BrowserViewport(1440, 900), [], AccountVerified: true, CanOpen: true);

		public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
		public Task<BrowserAccount> GetAccountAsync(string ssoUser, CancellationToken cancellationToken = default) =>
			Task.FromResult(new BrowserAccount("7c79d5435d249ca9", "codeassist.04@easybuy.co.th"));
		public Task<BrowserSessionState> CreateSessionAsync(string ssoUser, CancellationToken cancellationToken = default) => Task.FromResult(ReadySession);
		public Task<BrowserSessionState?> GetCurrentSessionAsync(string ssoUser, CancellationToken cancellationToken = default) => Task.FromResult<BrowserSessionState?>(null);
		public Task<BrowserSessionState?> GetSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default) => Task.FromResult<BrowserSessionState?>(ReadySession);
		public Task<BrowserSessionState> OpenSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default) =>
			Task.FromResult(ReadySession with { Status = "handed-off" });
		public Task EndSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default) => Task.CompletedTask;
	}
}
