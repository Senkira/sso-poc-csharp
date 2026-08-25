using System;
using System.Net;

namespace SsoGeminiLogin.Mvc.Integrations.Interfaces;

public sealed class BrowserBrokerException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null, bool isPublic = false) : Exception(message, innerException)
{
	public HttpStatusCode? StatusCode { get; } = statusCode;

	public bool IsPublic { get; } = isPublic;
}

