using System;

namespace SsoGeminiLogin.Api.Integrations.Interfaces;

public sealed class AccountBusyException : Exception
{
	public AccountBusyException(string message)
		: base(message)
	{
	}
}

