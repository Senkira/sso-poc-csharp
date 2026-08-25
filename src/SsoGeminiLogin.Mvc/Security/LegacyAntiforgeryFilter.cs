using System;
using System.Collections.Frozen;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SsoGeminiLogin.Mvc.Security;

public sealed class LegacyAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter, IFilterMetadata, IOrderedFilter
{
	private static readonly FrozenSet<string> SafeMethods = new string[4]
	{
		HttpMethods.Get,
		HttpMethods.Head,
		HttpMethods.Options,
		HttpMethods.Trace
	}.ToFrozenSet<string>(StringComparer.OrdinalIgnoreCase);

	public int Order => 1000;

	public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
	{
		if (SafeMethods.Contains(context.HttpContext.Request.Method))
		{
			return;
		}
		try
		{
			await antiforgery.ValidateRequestAsync(context.HttpContext);
		}
		catch (AntiforgeryValidationException)
		{
			bool flag = context.HttpContext.Request.Path.Equals("/sso/login", StringComparison.OrdinalIgnoreCase);
			context.Result = new JsonResult(new
			{
				error = (flag ? "Open the login page before signing in." : "CSRF validation failed.")
			})
			{
				StatusCode = 403
			};
		}
	}
}

