using System.Threading;
using System.Threading.Tasks;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Integrations.Interfaces;

public interface IBrowserAgent
{
	Task<BrowserAgentDescription> DescribeAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserAgentStatus> StartAsync(string user, BrowserAccount account, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserAgentStatus> StatusAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task<BrowserAgentStatus> HandoffAsync(string user, CancellationToken cancellationToken = default(CancellationToken));

	Task EndAsync(string user, CancellationToken cancellationToken = default(CancellationToken));
}

