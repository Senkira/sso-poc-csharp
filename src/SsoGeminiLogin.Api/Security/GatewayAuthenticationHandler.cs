using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;

namespace SsoGeminiLogin.Api.Security;

public sealed class GatewayAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions, ILoggerFactory logger, UrlEncoder encoder, IOptions<GatewayOptions> gatewayOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
	public const string SchemeName = "TrustedGateway";

	public const string GatewayKeyHeader = "X-Gateway-Key";

	public const string SsoUserHeader = "X-SSO-User";

	private readonly string _sharedKey = gatewayOptions.Value.SharedKey;

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		string supplied = base.Request.Headers["X-Gateway-Key"].ToString();
		if (string.IsNullOrWhiteSpace(_sharedKey) || !FixedTimeEquals(supplied, _sharedKey))
		{
			return Task.FromResult(AuthenticateResult.Fail("Trusted MVC gateway is required."));
		}
		string text = base.Request.Headers["X-SSO-User"].ToString().Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(text) || text.Length > 320)
		{
			return Task.FromResult(AuthenticateResult.Fail("Verified SSO identity is required."));
		}
		Claim[] claims = new Claim[2]
		{
			new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", text),
			new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", text)
		};
		ClaimsIdentity identity = new ClaimsIdentity(claims, "TrustedGateway");
		ClaimsPrincipal principal = new ClaimsPrincipal(identity);
		AuthenticationTicket ticket = new AuthenticationTicket(principal, "TrustedGateway");
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}

	protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
	{
		base.Response.StatusCode = 401;
		base.Response.ContentType = "application/problem+json";
		ProblemDetails problemDetails = new ProblemDetails
		{
			Status = 401,
			Title = "Authentication is required.",
			Type = "https://httpstatuses.com/401",
			Instance = base.Request.Path
		};
		problemDetails.Extensions["traceId"] = base.Context.TraceIdentifier;
		await base.Response.WriteAsJsonAsync(problemDetails, new JsonSerializerOptions(JsonSerializerDefaults.Web), "application/problem+json");
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

