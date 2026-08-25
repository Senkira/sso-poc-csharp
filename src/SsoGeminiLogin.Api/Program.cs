using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;
using SsoGeminiLogin.Api.Health;
using SsoGeminiLogin.Api.Integrations.AgentProcess;
using SsoGeminiLogin.Api.Integrations.AppSettingsMockDatabase;
using SsoGeminiLogin.Api.Integrations.Interfaces;
using SsoGeminiLogin.Api.Integrations.SimulatedBrowser;
using SsoGeminiLogin.Api.Middleware;
using SsoGeminiLogin.Api.Security;
using SsoGeminiLogin.Api.Services;
using SsoGeminiLogin.Api.Services.Interfaces;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Host.UseDefaultServiceProvider((context, options) =>
{
	options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
	options.ValidateOnBuild = true;
});

builder.Services.AddOptions<AgentOptions>()
	.Bind(builder.Configuration.GetSection("Agent"))
	.Validate(options => options.MaxWorkers > 0, "Agent:MaxWorkers must be greater than zero.")
	.Validate(options => options.Mode is "Simulated" or "Local", "Agent:Mode must be Simulated or Local.")
	.Validate(options => options.Mode != "Local" ||
		(!string.IsNullOrWhiteSpace(options.ExpectedAccountId) && !string.IsNullOrWhiteSpace(options.ExpectedAccountEmail)),
		"Agent:ExpectedAccountId and Agent:ExpectedAccountEmail are required in Local mode.")
	.ValidateOnStart();

builder.Services.AddOptions<GatewayOptions>()
	.Bind(builder.Configuration.GetSection("Gateway"))
	.Validate(options => options.SharedKey.Length >= 32, "Gateway:SharedKey must contain at least 32 characters.")
	.Validate(options => builder.Environment.IsDevelopment() || options.SharedKey != "local-dev-gateway-key-change-before-deployment",
		"Gateway:SharedKey must be replaced outside Development.")
	.ValidateOnStart();

builder.Services.AddOptions<BrowserSessionOptions>()
	.Bind(builder.Configuration.GetSection("BrowserSessions"))
	.Validate(options => options.TtlMinutes is >= 1 and <= 1440, "BrowserSessions:TtlMinutes must be between 1 and 1440.")
	.ValidateOnStart();

builder.Services.AddOptions<MockDatabaseOptions>()
	.Bind(builder.Configuration.GetSection("MockDatabase"))
	.Validate(options => string.Equals(options.Provider, "AppSettings", StringComparison.OrdinalIgnoreCase), "MockDatabase:Provider must be AppSettings.")
	.Validate(options => options.SimulatedLatencyMilliseconds is >= 0 and <= 5000, "MockDatabase:SimulatedLatencyMilliseconds must be between 0 and 5000.")
	.Validate(options => options.AccountMappings.Count > 0, "MockDatabase:AccountMappings must contain at least one mapping.")
	.Validate(options => options.AccountMappings.All(mapping =>
		!string.IsNullOrWhiteSpace(mapping.SsoUser) &&
		!string.IsNullOrWhiteSpace(mapping.AccountId) &&
		!string.IsNullOrWhiteSpace(mapping.GeminiAccountEmail)),
		"Every mock account mapping must contain SsoUser, AccountId and GeminiAccountEmail.")
	.Validate(options => options.AccountMappings.Select(mapping => mapping.SsoUser.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == options.AccountMappings.Count,
		"MockDatabase:SsoUser values must be unique.")
	.ValidateOnStart();

builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
	options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAuthentication("TrustedGateway")
	.AddScheme<AuthenticationSchemeOptions, GatewayAuthenticationHandler>("TrustedGateway", _ => { });
builder.Services.AddAuthorization(options =>
	options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBrowserAccountMappingRepository, AppSettingsBrowserAccountMappingRepository>();
builder.Services.AddSingleton<BrowserSessionStore>();
builder.Services.AddSingleton<IBrowserAgent>(services =>
{
	AgentOptions options = services.GetRequiredService<IOptions<AgentOptions>>().Value;
	return string.Equals(options.Mode, "Simulated", StringComparison.OrdinalIgnoreCase)
		? ActivatorUtilities.CreateInstance<SimulatedBrowserAgent>(services)
		: ActivatorUtilities.CreateInstance<AgentProcessBrowserAgent>(services);
});
builder.Services.AddSingleton<BrowserAgentStartupValidator>();
builder.Services.AddScoped<IBrowserSessionService, BrowserSessionService>();
builder.Services.AddHealthChecks()
	.AddCheck<MockDatabaseHealthCheck>("mock-database", tags: ["ready"])
	.AddCheck<BrowserAgentHealthCheck>("browser-agent", tags: ["agent"]);

WebApplication app = builder.Build();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
	Predicate = _ => false,
	ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = registration => registration.Tags.Contains("ready"),
	ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapHealthChecks("/health/agent", new HealthCheckOptions
{
	Predicate = registration => registration.Tags.Contains("agent"),
	ResponseWriter = HealthResponseWriter.WriteAsync
}).AllowAnonymous();
app.MapControllers();
app.Run();

public partial class Program;
