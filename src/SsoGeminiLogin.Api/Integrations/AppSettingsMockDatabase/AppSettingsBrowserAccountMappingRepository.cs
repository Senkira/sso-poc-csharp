using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;
using SsoGeminiLogin.Api.Integrations.Interfaces;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Integrations.AppSettingsMockDatabase;

public sealed class AppSettingsBrowserAccountMappingRepository(IOptionsMonitor<MockDatabaseOptions> options, ILogger<AppSettingsBrowserAccountMappingRepository> logger) : IBrowserAccountMappingRepository
{
	public const string DataSourceName = "MockDatabase:AppSettings";

	private static readonly Action<ILogger, string, bool, Exception?> LogQueryCompleted = LoggerMessage.Define<string, bool>(LogLevel.Information, new EventId(2101, "FindActiveBySsoUserAsync"), "Mock database query FindActiveAccountMapping completed. Provider={Provider}; Found={Found}");

	public async Task<AccountMappingResult?> FindActiveBySsoUserAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken))
	{
		MockDatabaseOptions settings = options.CurrentValue;
		int num = Math.Clamp(settings.SimulatedLatencyMilliseconds, 0, 5000);
		if (num > 0)
		{
			await Task.Delay(num, cancellationToken);
		}
		string normalizedUser = ssoUser.Trim().ToLowerInvariant();
		MockAccountMappingOptions? mockAccountMappingOptions = settings.AccountMappings.FirstOrDefault((MockAccountMappingOptions candidate) => candidate.Enabled && string.Equals(candidate.SsoUser.Trim(), normalizedUser, StringComparison.OrdinalIgnoreCase));
		LogQueryCompleted(logger, settings.Provider, mockAccountMappingOptions != null, null);
		if (mockAccountMappingOptions == null)
		{
			return null;
		}
		return new AccountMappingResult(new BrowserAccount(mockAccountMappingOptions.AccountId.Trim(), mockAccountMappingOptions.GeminiAccountEmail.Trim().ToLowerInvariant()), "MockDatabase:AppSettings");
	}
}
