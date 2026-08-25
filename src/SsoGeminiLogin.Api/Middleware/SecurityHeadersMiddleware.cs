using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SsoGeminiLogin.Api.Middleware;

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
		await next(context);
	}
}

