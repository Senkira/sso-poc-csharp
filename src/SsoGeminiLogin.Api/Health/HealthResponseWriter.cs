using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SsoGeminiLogin.Api.Health;

public static class HealthResponseWriter
{
	public static Task WriteAsync(HttpContext context, HealthReport report)
	{
		context.Response.ContentType = "application/json; charset=utf-8";
		var value = new
		{
			status = report.Status.ToString().ToLowerInvariant(),
			service = "gemini-session-broker",
			checks = Enumerable.ToDictionary(report.Entries, (KeyValuePair<string, HealthReportEntry> entry) => entry.Key, (KeyValuePair<string, HealthReportEntry> entry) => new
			{
				status = entry.Value.Status.ToString().ToLowerInvariant(),
				description = entry.Value.Description
			})
		};
		return context.Response.WriteAsync(JsonSerializer.Serialize(value));
	}
}

