using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SsoGeminiLogin.Api.IntegrationTest;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewayKey = "integration-test-gateway-key-1234567890";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:SharedKey"] = GatewayKey,
				["Agent:Mode"] = "Simulated",
                ["MockDatabase:SimulatedLatencyMilliseconds"] = "0"
            });
        });
    }
}
