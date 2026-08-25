using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;

namespace SsoGeminiLogin.Api.Health;

public sealed class MockDatabaseHealthCheck(IOptionsMonitor<MockDatabaseOptions> options) : IHealthCheck
{
	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
	{
		MockDatabaseOptions currentValue = options.CurrentValue;
		int num = currentValue.AccountMappings.Count((MockAccountMappingOptions mapping) => mapping.Enabled);
		if (!string.Equals(currentValue.Provider, "AppSettings", StringComparison.OrdinalIgnoreCase))
		{
			return Task.FromResult(HealthCheckResult.Unhealthy("Mock database provider must be AppSettings."));
		}
		if (num == 0)
		{
			return Task.FromResult(HealthCheckResult.Unhealthy("Mock database has no active account mappings."));
		}
		return Task.FromResult(HealthCheckResult.Healthy($"{"MockDatabase:AppSettings"} is ready with {num} active mapping(s)."));
	}
}

