using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SsoGeminiLogin.Mvc.Integrations.Interfaces;

namespace SsoGeminiLogin.Mvc.Integrations.BrowserBroker;

public sealed class BrowserBrokerHealthCheck(IBrowserBrokerClient browserBroker) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
	{
		return (await browserBroker.IsReadyAsync(cancellationToken)) ? HealthCheckResult.Healthy("The browser broker API is ready.") : HealthCheckResult.Unhealthy("The browser broker API is unavailable.");
	}
}

