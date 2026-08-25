using System.Threading;
using System.Threading.Tasks;

namespace SsoGeminiLogin.Mvc.Integrations.Interfaces;

public interface IBrowserBrokerClient
{
	Task<bool> IsReadyAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserAccount> GetAccountAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState> CreateSessionAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState?> GetCurrentSessionAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState?> GetSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState> OpenSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default(CancellationToken));

	Task EndSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default(CancellationToken));
}

