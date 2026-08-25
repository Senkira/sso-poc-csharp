using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SsoGeminiLogin.Api.Health;

public sealed class BrowserAgentHealthCheck(BrowserAgentStartupValidator startupValidator) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			return HealthCheckResult.Healthy("Local Browser Agent is ready for " + (await startupValidator.ValidateAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Account.Email + ".");
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			string description = ((ex is InvalidOperationException && ex.Message.StartsWith("Local Browser Agent credential does not match", StringComparison.Ordinal)) ? ex.Message : "Local Browser Agent is unavailable.");
			return HealthCheckResult.Unhealthy(description, ex);
		}
	}
}

