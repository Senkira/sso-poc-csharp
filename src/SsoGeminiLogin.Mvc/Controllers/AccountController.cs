using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Mvc.Integrations.BrowserBroker;
using SsoGeminiLogin.Mvc.Models.ViewModels;

namespace SsoGeminiLogin.Mvc.Controllers;

public sealed class AccountController(IOptions<DevSsoOptions> options) : Controller
{
	private readonly DevSsoOptions _options = options.Value;

	[AllowAnonymous]
	[HttpGet("/")]
	public IActionResult Login([FromQuery] int? error = null)
	{
		return View(new LoginViewModel
		{
			InvalidCredentials = (error == 1)
		});
	}

	[AllowAnonymous]
	[HttpPost("/sso/login")]
	public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
	{
		if (!_options.Enabled)
		{
			return NotFound();
		}
		if (!base.ModelState.IsValid || !FixedTimeEquals(model.Username.Trim(), _options.Username) || !FixedTimeEquals(model.Password, _options.Password))
		{
			return Redirect("/?error=1");
		}
		string value = _options.MappedUser.Trim().ToLowerInvariant();
		Claim[] claims = new Claim[2]
		{
			new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", value),
			new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", value)
		};
		ClaimsIdentity identity = new ClaimsIdentity(claims, "Cookies");
		await base.HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(identity), new AuthenticationProperties
		{
			IsPersistent = true,
			AllowRefresh = false,
			ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1.0)
		});
		cancellationToken.ThrowIfCancellationRequested();
		return Redirect("/viewer");
	}

	private static bool FixedTimeEquals(string supplied, string expected)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(supplied);
		byte[] bytes2 = Encoding.UTF8.GetBytes(expected);
		if (bytes.Length == bytes2.Length)
		{
			return CryptographicOperations.FixedTimeEquals(bytes, bytes2);
		}
		return false;
	}
}

