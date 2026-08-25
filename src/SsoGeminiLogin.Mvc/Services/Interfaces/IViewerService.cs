using System.Threading;
using System.Threading.Tasks;
using SsoGeminiLogin.Mvc.Integrations.Interfaces;

namespace SsoGeminiLogin.Mvc.Services.Interfaces;

public interface IViewerService
{
	Task<BrowserAccount> GetAccountAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState?> GetCurrentSessionAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState?> GetSessionAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState> StartAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionState> OpenAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken));

	Task EndAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken));
}

