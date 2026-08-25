using System.Threading;
using System.Threading.Tasks;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Services.Interfaces;

public interface IBrowserSessionService
{
	Task<AccountMappingResult?> GetAccountMappingAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionResult?> CreateAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionResult?> GetCurrentAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionResult?> GetAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserSessionResult?> OpenAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<bool> EndAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken));
}

