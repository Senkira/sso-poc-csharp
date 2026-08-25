using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SsoGeminiLogin.Mvc.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
	public async Task InvokeAsync(HttpContext context)
	{
		IHeaderDictionary headers = context.Response.Headers;
		headers.CacheControl = "no-store";
		headers.Append("Referrer-Policy", "no-referrer");
		headers.Append("X-Content-Type-Options", "nosniff");
		headers.Append("X-Frame-Options", "DENY");
		headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
		headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'");
		await next(context);
	}
}

