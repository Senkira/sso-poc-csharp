using System.Threading;
using System.Threading.Tasks;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Integrations.Interfaces;

public interface IBrowserAccountMappingRepository
{
	Task<AccountMappingResult?> FindActiveBySsoUserAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken));
}

