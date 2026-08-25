using System;

namespace SsoGeminiLogin.Api.Integrations.Interfaces;

public sealed class BrowserCapacityException : Exception
{
	public BrowserCapacityException(string message)
		: base(message)
	{
	}
}

