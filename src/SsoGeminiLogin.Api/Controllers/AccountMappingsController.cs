using System;
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
[Route("api/v1/account-mappings")]
[Produces("application/json", new string[] { })]
public sealed class AccountMappingsController(IBrowserSessionService browserSessions) : ControllerBase
{
	[HttpGet("current")]
	[ProducesResponseType<AccountMappingResponse>(200)]
	[ProducesResponseType<ProblemDetails>(404)]
	public async Task<ActionResult<AccountMappingResponse>> GetCurrent(CancellationToken cancellationToken)
	{
		AccountMappingResult? accountMappingResult = await browserSessions.GetAccountMappingAsync(CurrentUser(), cancellationToken);
		if (accountMappingResult is null)
		{
			return Problem(null, null, 404, "No active account mapping was found.");
		}
		return Ok(new AccountMappingResponse(new AccountResponse(accountMappingResult.Account.Id, accountMappingResult.Account.Email), accountMappingResult.DataSource));
	}

	private string CurrentUser()
	{
		return base.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? throw new InvalidOperationException("Authenticated gateway identity is unavailable.");
	}
}
