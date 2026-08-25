using System.Threading;
using System.Threading.Tasks;
using SsoGeminiLogin.Mvc.Integrations.Interfaces;
using SsoGeminiLogin.Mvc.Services.Interfaces;

namespace SsoGeminiLogin.Mvc.Services;

public sealed class ViewerService(IBrowserBrokerClient browserBroker) : IViewerService
{
	public Task<BrowserAccount> GetAccountAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return browserBroker.GetAccountAsync(user, cancellationToken);
	}

	public Task<BrowserSessionState?> GetCurrentSessionAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return browserBroker.GetCurrentSessionAsync(user, cancellationToken);
	}

	public Task<BrowserSessionState?> GetSessionAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return browserBroker.GetSessionAsync(sessionId, user, cancellationToken);
	}

	public Task<BrowserSessionState> StartAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return browserBroker.CreateSessionAsync(user, cancellationToken);
	}

	public Task<BrowserSessionState> OpenAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return browserBroker.OpenSessionAsync(sessionId, user, cancellationToken);
	}

	public Task EndAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return browserBroker.EndSessionAsync(sessionId, user, cancellationToken);
	}
}

