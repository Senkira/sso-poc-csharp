using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Mvc.Integrations.BrowserBroker;

namespace SsoGeminiLogin.Mvc.UnitTest.Integrations.AccountMapping;

public sealed class BrowserBrokerClientTests
{
	private const string GatewayKey = "unit-test-gateway-key-123456789012345";

	[Fact]
	public async Task GetAccountAsyncMapsResponseAndSendsTrustedHeaders()
	{
		RecordingHandler handler = new(HttpStatusCode.OK, """{"account":{"id":"account-1","email":"mapped@example.test"}}""");
		BrowserBrokerClient client = CreateClient(handler);

		var result = await client.GetAccountAsync("USER@EXAMPLE.TEST");

		Assert.Equal("account-1", result.Id);
		Assert.Equal("mapped@example.test", result.Email);
		Assert.Equal(GatewayKey, handler.GatewayKey);
		Assert.Equal("user@example.test", handler.SsoUser);
		Assert.Equal("/api/v1/account-mappings/current", handler.Path);
	}

	[Fact]
	public async Task CreateSessionAsyncPreservesOpaqueSessionId()
	{
		BrowserBrokerClient client = CreateClient(new RecordingHandler(HttpStatusCode.Accepted, """{"sessionId":"gbs_test","status":"starting"}"""));
		var result = await client.CreateSessionAsync("user@example.test");
		Assert.Equal("gbs_test", result.SessionId);
		Assert.Equal("starting", result.Status);
	}

	private static BrowserBrokerClient CreateClient(HttpMessageHandler handler)
	{
		HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://api.test"), Timeout = TimeSpan.FromSeconds(5) };
		return new BrowserBrokerClient(httpClient, Options.Create(new BrowserBrokerOptions
		{
			BaseUrl = "http://api.test",
			GatewayKey = GatewayKey,
			TimeoutSeconds = 5,
			MaximumResponseBytes = 1_048_576
		}));
	}

	private sealed class RecordingHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
	{
		public string? GatewayKey { get; private set; }
		public string? SsoUser { get; private set; }
		public string? Path { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			GatewayKey = request.Headers.GetValues("X-Gateway-Key").Single();
			SsoUser = request.Headers.GetValues("X-SSO-User").Single();
			Path = request.RequestUri?.AbsolutePath;
			return Task.FromResult(new HttpResponseMessage(statusCode)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json")
			});
		}
	}
}
