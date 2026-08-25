using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;
using SsoGeminiLogin.Api.Integrations.Interfaces;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Health;

public sealed class BrowserAgentStartupValidator(IBrowserAgent browserAgent, IOptions<AgentOptions> options)
{
	public async Task<BrowserAgentDescription> ValidateAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		BrowserAgentDescription browserAgentDescription = await browserAgent.DescribeAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		AgentOptions value = options.Value;
		if (string.Equals(value.Mode, "Local", StringComparison.OrdinalIgnoreCase) && (!string.Equals(browserAgentDescription.Account.Id, value.ExpectedAccountId, StringComparison.Ordinal) || !string.Equals(browserAgentDescription.Account.Email, value.ExpectedAccountEmail, StringComparison.OrdinalIgnoreCase)))
		{
			throw new InvalidOperationException("Local Browser Agent credential does not match the configured Gemini account mapping.");
		}
		return browserAgentDescription;
	}
}

