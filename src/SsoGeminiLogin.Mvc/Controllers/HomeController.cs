using System;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SsoGeminiLogin.Mvc.Integrations.Interfaces;
using SsoGeminiLogin.Mvc.Models.ViewModels;
using SsoGeminiLogin.Mvc.Services.Interfaces;

namespace SsoGeminiLogin.Mvc.Controllers;

[Authorize]
public sealed class HomeController(IViewerService viewerService, IAntiforgery antiforgery) : Controller
{
	[AllowAnonymous]
	[HttpGet("/healthz")]
	public IActionResult LegacyHealth()
	{
		return StatusCode(200, new
		{
			service = "gemini-session-broker",
			status = "ok"
		});
	}

	[HttpGet("/viewer")]
	[HttpGet("/gemini")]
	public IActionResult Viewer([FromQuery] bool stopped = false)
	{
		return View(new ViewerViewModel
		{
			SsoUser = CurrentUser(),
			AccountEmail = "—",
			WasStopped = stopped
		});
	}

	[HttpGet("/api/v1/me")]
	public async Task<IActionResult> Me(CancellationToken cancellationToken)
	{
		string user = CurrentUser();
		BrowserAccount account = await viewerService.GetAccountAsync(user, cancellationToken);
		string csrfToken = antiforgery.GetAndStoreTokens(base.HttpContext).RequestToken ?? throw new InvalidOperationException("An antiforgery request token could not be created.");
		return StatusCode(200, new
		{
			user = user,
			account = account,
			csrfToken = csrfToken,
			architecture = "broker-agent"
		});
	}

	[HttpPost("/api/v1/browser-sessions")]
	public async Task<IActionResult> Start(CancellationToken cancellationToken)
	{
		BrowserSessionState browserSessionState = await viewerService.StartAsync(CurrentUser(), cancellationToken);
		return Accepted(new
		{
			sessionId = browserSessionState.SessionId,
			status = browserSessionState.Status,
			statusUrl = "/api/v1/browser-sessions/" + browserSessionState.SessionId
		});
	}

	[HttpGet("/api/v1/browser-sessions/current")]
	public async Task<IActionResult> Current(CancellationToken cancellationToken)
	{
		BrowserSessionState? browserSessionState = await viewerService.GetCurrentSessionAsync(CurrentUser(), cancellationToken);
		return browserSessionState is null ? NotFound(new
		{
			error = "No browser session."
		}) : StatusCode(200, new { browserSessionState.SessionId, browserSessionState.Status, browserSessionState.Message, browserSessionState.Account, browserSessionState.CreatedAt, browserSessionState.Viewport, browserSessionState.Progress, browserSessionState.AccountVerified });
	}

	[HttpGet("/api/v1/browser-sessions/{sessionId}")]
	public async Task<IActionResult> Status(string sessionId, CancellationToken cancellationToken)
	{
		BrowserSessionState? browserSessionState = await viewerService.GetSessionAsync(sessionId, CurrentUser(), cancellationToken);
		return browserSessionState is null ? NotFound(new
		{
			error = "Browser session was not found."
		}) : StatusCode(200, new
		{
			SessionId = browserSessionState.SessionId,
			user = CurrentUser(),
			Status = browserSessionState.Status,
			Message = browserSessionState.Message,
			Account = browserSessionState.Account,
			CreatedAt = browserSessionState.CreatedAt,
			Viewport = browserSessionState.Viewport,
			Progress = browserSessionState.Progress,
			AccountVerified = browserSessionState.AccountVerified,
			openAllowed = browserSessionState.CanOpen
		});
	}

	[HttpPost("/api/v1/browser-sessions/{sessionId}/open")]
	public async Task<IActionResult> Open(string sessionId, CancellationToken cancellationToken)
	{
		BrowserSessionState browserSessionState = await viewerService.OpenAsync(sessionId, CurrentUser(), cancellationToken);
		return StatusCode(200, new { browserSessionState.SessionId, browserSessionState.Status, browserSessionState.Message, browserSessionState.Account, browserSessionState.CreatedAt, browserSessionState.Viewport, browserSessionState.Progress, browserSessionState.AccountVerified });
	}

	[HttpDelete("/api/v1/browser-sessions/{sessionId}")]
	public async Task<IActionResult> End(string sessionId, CancellationToken cancellationToken)
	{
		await viewerService.EndAsync(sessionId, CurrentUser(), cancellationToken);
		return StatusCode(200, new
		{
			ended = true
		});
	}

	[AllowAnonymous]
	[Route("/error")]
	public IActionResult Error()
	{
		IExceptionHandlerPathFeature? exceptionHandlerPathFeature = base.HttpContext.Features.Get<IExceptionHandlerPathFeature>();
		BrowserBrokerException? ex = exceptionHandlerPathFeature?.Error as BrowserBrokerException;
		HttpStatusCode? httpStatusCode = ex?.StatusCode;
		int num;
		if (!httpStatusCode.HasValue)
		{
			num = ((ex != null) ? 502 : 500);
		}
		else
		{
			HttpStatusCode valueOrDefault = httpStatusCode.GetValueOrDefault();
			num = (int)valueOrDefault;
		}
		int statusCode = num;
		base.Response.StatusCode = statusCode;
		if (exceptionHandlerPathFeature != null && exceptionHandlerPathFeature.Path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
		{
			return StatusCode(statusCode, new
			{
				error = ((ex != null && ex.IsPublic) ? ex.Message : "The browser broker API is temporarily unavailable.")
			});
		}
		return View(new ErrorViewModel
		{
			TraceId = (Activity.Current?.Id ?? base.HttpContext.TraceIdentifier),
			StatusCode = statusCode,
			Message = ((ex != null && ex.IsPublic) ? ex.Message : ((ex != null) ? "The browser broker API is temporarily unavailable." : "The request could not be completed."))
		});
	}

	private string CurrentUser()
	{
		return base.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? throw new InvalidOperationException("Authenticated SSO identity is unavailable.");
	}
}
