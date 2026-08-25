using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Mvc.Integrations.BrowserBroker;
using SsoGeminiLogin.Mvc.Integrations.Interfaces;
using SsoGeminiLogin.Mvc.Middleware;
using SsoGeminiLogin.Mvc.Security;
using SsoGeminiLogin.Mvc.Services;
using SsoGeminiLogin.Mvc.Services.Interfaces;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
bool allowsLocalHttp = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("LocalPoc");
if (allowsLocalHttp)
{
	string? keysPath = builder.Configuration["DataProtection:KeysPath"];
	if (string.IsNullOrWhiteSpace(keysPath))
	{
		builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
	}
	else
	{
		builder.Services.AddDataProtection()
			.PersistKeysToFileSystem(new DirectoryInfo(keysPath))
			.SetApplicationName("SsoGeminiLogin.Mvc");
	}
}
builder.Host.UseDefaultServiceProvider((context, options) =>
{
	options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
	options.ValidateOnBuild = true;
});

builder.Services.AddOptions<BrowserBrokerOptions>()
	.Bind(builder.Configuration.GetSection("BrokerApi"))
	.Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "BrokerApi:BaseUrl must be an absolute URL.")
	.Validate(options => options.GatewayKey.Length >= 32, "BrokerApi:GatewayKey must contain at least 32 characters.")
	.Validate(options => options.TimeoutSeconds is >= 1 and <= 300, "BrokerApi:TimeoutSeconds must be between 1 and 300.")
	.Validate(options => options.MaximumResponseBytes is >= 1024 and <= 10_485_760, "BrokerApi:MaximumResponseBytes must be between 1024 and 10485760.")
	.Validate(options => builder.Environment.IsDevelopment() || options.GatewayKey != "local-dev-gateway-key-change-before-deployment",
		"BrokerApi:GatewayKey must be replaced outside Development.")
	.ValidateOnStart();

builder.Services.AddOptions<DevSsoOptions>()
	.Bind(builder.Configuration.GetSection("DevSso"))
	.Validate(options => allowsLocalHttp || !options.Enabled, "DevSso must be disabled outside Development or the isolated localhost POC environment.")
	.Validate(options => !options.Enabled ||
		(!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password) && !string.IsNullOrWhiteSpace(options.MappedUser)),
		"DevSso credentials and mapped user are required when the demo identity provider is enabled.")
	.ValidateOnStart();

builder.Services.AddControllersWithViews(options => options.Filters.Add<LegacyAntiforgeryFilter>());
builder.Services.AddAntiforgery(options =>
{
	options.HeaderName = "X-CSRF-Token";
	options.Cookie.Name = "poc_csrf";
	options.Cookie.HttpOnly = true;
	options.Cookie.SameSite = SameSiteMode.Strict;
	options.Cookie.SecurePolicy = allowsLocalHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication("Cookies").AddCookie(options =>
{
	options.Cookie.Name = "broker_id";
	options.Cookie.HttpOnly = true;
	options.Cookie.SameSite = SameSiteMode.Strict;
	options.Cookie.SecurePolicy = allowsLocalHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
	options.Cookie.Path = "/";
	options.Cookie.MaxAge = TimeSpan.FromHours(1);
	options.LoginPath = "/";
	options.ExpireTimeSpan = TimeSpan.FromHours(1);
	options.SlidingExpiration = false;
	options.Events.OnRedirectToLogin = context =>
	{
		if (context.Request.Path.StartsWithSegments("/api/v1"))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return context.Response.WriteAsJsonAsync(new { error = "Verified SSO session is required." });
		}
		context.Response.Redirect("/");
		return Task.CompletedTask;
	};
});
builder.Services.AddAuthorization();
builder.Services.AddHttpClient<IBrowserBrokerClient, BrowserBrokerClient>((services, client) =>
{
	BrowserBrokerOptions options = services.GetRequiredService<IOptions<BrowserBrokerOptions>>().Value;
	client.BaseAddress = new Uri(options.BaseUrl);
	client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<IViewerService, ViewerService>();
builder.Services.AddHealthChecks().AddCheck<BrowserBrokerHealthCheck>("browser-broker", tags: ["ready"]);

WebApplication app = builder.Build();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllerRoute("default", "{controller=Account}/{action=Login}/{id?}");
app.Run();

public partial class Program;
