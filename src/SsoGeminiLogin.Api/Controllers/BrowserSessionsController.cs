using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SsoGeminiLogin.Api.Models.Responses;
using SsoGeminiLogin.Api.Services.Interfaces;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/browser-sessions")]
[Produces("application/json", new string[] { })]
public sealed class BrowserSessionsController(IBrowserSessionService browserSessions) : ControllerBase
{
	[HttpPost]
	[ProducesResponseType<BrowserSessionCreatedResponse>(202)]
	[ProducesResponseType<ProblemDetails>(404)]
	[ProducesResponseType<ProblemDetails>(409)]
	[ProducesResponseType<ProblemDetails>(503)]
	public async Task<ActionResult<BrowserSessionCreatedResponse>> Create(CancellationToken cancellationToken)
	{
		BrowserSessionResult? browserSessionResult = await browserSessions.CreateAsync(CurrentUser(), cancellationToken);
		if (browserSessionResult is null)
		{
			return Problem(null, null, 404, "No active account mapping was found.");
		}
		BrowserSessionCreatedResponse value = new BrowserSessionCreatedResponse(browserSessionResult.SessionId, browserSessionResult.Status, base.Url.ActionLink("Get", null, new
		{
			sessionId = browserSessionResult.SessionId
		}) ?? ("/api/v1/browser-sessions/" + browserSessionResult.SessionId));
		return AcceptedAtAction("Get", new
		{
			sessionId = browserSessionResult.SessionId
		}, value);
	}

	[HttpGet("current")]
	[ProducesResponseType<BrowserSessionResponse>(200)]
	[ProducesResponseType<ProblemDetails>(404)]
	public async Task<ActionResult<BrowserSessionResponse>> GetCurrent(CancellationToken cancellationToken)
	{
		BrowserSessionResult? browserSessionResult = await browserSessions.GetCurrentAsync(CurrentUser(), cancellationToken);
		return browserSessionResult is null ? Problem(null, null, 404, "No browser session was found.") : Ok(ToResponse(browserSessionResult));
	}

	[HttpGet("{sessionId}")]
	[ProducesResponseType<BrowserSessionResponse>(200)]
	[ProducesResponseType<ProblemDetails>(404)]
	public async Task<ActionResult<BrowserSessionResponse>> Get(string sessionId, CancellationToken cancellationToken)
	{
		BrowserSessionResult? browserSessionResult = await browserSessions.GetAsync(sessionId, CurrentUser(), cancellationToken);
		return browserSessionResult is null ? Problem(null, null, 404, "Browser session was not found.") : Ok(ToResponse(browserSessionResult));
	}

	[HttpPost("{sessionId}/open")]
	[ProducesResponseType<BrowserSessionResponse>(200)]
	[ProducesResponseType<ProblemDetails>(404)]
	[ProducesResponseType<ProblemDetails>(409)]
	public async Task<ActionResult<BrowserSessionResponse>> Open(string sessionId, CancellationToken cancellationToken)
	{
		BrowserSessionResult? browserSessionResult = await browserSessions.OpenAsync(sessionId, CurrentUser(), cancellationToken);
		return browserSessionResult is null ? Problem(null, null, 404, "Browser session was not found.") : Ok(ToResponse(browserSessionResult));
	}

	[HttpDelete("{sessionId}")]
	[ProducesResponseType(200)]
	[ProducesResponseType<ProblemDetails>(404)]
	public async Task<IActionResult> Delete(string sessionId, CancellationToken cancellationToken)
	{
		return (await browserSessions.EndAsync(sessionId, CurrentUser(), cancellationToken)) ? Ok(new
		{
			ended = true
		}) : Problem(null, null, 404, "Browser session was not found.");
	}

	private string CurrentUser()
	{
		return base.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? throw new InvalidOperationException("Authenticated gateway identity is unavailable.");
	}

	private static BrowserSessionResponse ToResponse(BrowserSessionResult result)
	{
		return new BrowserSessionResponse(result.SessionId, result.Status, result.Message, result.Account, result.CreatedAt, result.Viewport is null ? null : new AgentViewportResponse(result.Viewport.Width, result.Viewport.Height), result.Progress?.Select((AgentProgress progress) => new AgentProgressResponse(progress.Status, progress.Message, progress.At)).ToArray(), result.AccountVerified, result.OpenAllowed);
	}
}
