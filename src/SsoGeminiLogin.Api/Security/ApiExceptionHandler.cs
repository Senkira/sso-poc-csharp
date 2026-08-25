using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SsoGeminiLogin.Api.Integrations.Interfaces;

namespace SsoGeminiLogin.Api.Security;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
	private static readonly Action<ILogger, int, Exception?> LogUnhandledFailure = LoggerMessage.Define<int>(LogLevel.Error, new EventId(1101, "TryHandleAsync"), "Unhandled API request failed with status {StatusCode}.");

	private static readonly Action<ILogger, int, string, Exception?> LogRejectedRequest = LoggerMessage.Define<int, string>(LogLevel.Warning, new EventId(1102, "TryHandleAsync"), "API request was rejected with status {StatusCode} and error type {ErrorType}.");

	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		if (httpContext.Response.HasStarted)
		{
			return false;
		}
		var (num, title) = ((exception is AccountBusyException) ? (409, "Account is already in use.") : ((exception is BrowserCapacityException) ? (503, "Browser capacity is unavailable.") : ((exception is BrowserAgentUnavailableException) ? (503, "Local Browser Agent is unavailable or its Windows credential is missing.") : ((exception is InvalidOperationException) ? (409, "The browser session state is invalid.") : ((!(exception is OperationCanceledException) || !httpContext.RequestAborted.IsCancellationRequested) ? (500, "An unexpected broker error occurred.") : (499, "The client closed the request."))))));
		if (num >= 500)
		{
			LogUnhandledFailure(logger, num, exception);
		}
		else if (num != 499)
		{
			LogRejectedRequest(logger, num, exception.GetType().Name, null);
		}
		httpContext.Response.StatusCode = num;
		ProblemDetails problemDetails = new ProblemDetails
		{
			Status = num,
			Title = title,
			Type = $"https://httpstatuses.com/{num}",
			Instance = httpContext.Request.Path
		};
		problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
		return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
		{
			HttpContext = httpContext,
			ProblemDetails = problemDetails,
			Exception = exception
		});
	}
}

