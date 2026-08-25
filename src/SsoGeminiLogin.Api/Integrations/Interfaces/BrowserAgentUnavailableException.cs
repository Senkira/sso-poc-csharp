using System;

namespace SsoGeminiLogin.Api.Integrations.Interfaces;

public sealed class BrowserAgentUnavailableException : Exception
{
	public BrowserAgentUnavailableException(string message)
		: base(message)
	{
	}

	public BrowserAgentUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}

