using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SsoGeminiLogin.Api.IntegrationTest.Controllers;

public sealed class AccountMappingsControllerTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private const string MappedUser = "codeassist.04@easybuy.co.th";

    [Fact]
    public async Task ProtectedEndpointWithoutGatewayHeadersReturnsUnauthorizedProblem()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/account-mappings/current");
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(401, document!.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task TrustedGatewayReturnsConfiguredMapping()
    {
        using var client = CreateAuthorizedClient(MappedUser);

        using var response = await client.GetAsync("/api/v1/account-mappings/current");
        var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "codeassist.04@easybuy.co.th",
            document!.RootElement.GetProperty("account").GetProperty("email").GetString());
    }

    [Fact]
    public async Task UnknownIdentityReturnsNotFound()
    {
        using var client = CreateAuthorizedClient("missing@example.test");

        using var response = await client.GetAsync("/api/v1/account-mappings/current");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BrowserSessionEndpointCreatesOpaqueSession()
    {
        using var client = CreateAuthorizedClient(MappedUser);

        using var response = await client.PostAsync("/api/v1/browser-sessions", null);

		var document = await response.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
		Assert.StartsWith("gbs_", document!.RootElement.GetProperty("sessionId").GetString(), StringComparison.Ordinal);
    }

    private HttpClient CreateAuthorizedClient(string user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Gateway-Key", ApiWebApplicationFactory.GatewayKey);
        client.DefaultRequestHeaders.Add("X-SSO-User", user);
        return client;
    }
}
