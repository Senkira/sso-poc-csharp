using System.Net;
using System.Text.RegularExpressions;

namespace SsoGeminiLogin.Mvc.IntegrationTest.Controllers;

public sealed class AccountAndViewerControllerTests(MvcWebApplicationFactory factory) : IClassFixture<MvcWebApplicationFactory>
{
	[Fact]
	public async Task AnonymousViewerRedirectsToWebSsoLogin()
	{
		using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false });
		using HttpResponseMessage response = await client.GetAsync("/viewer");
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/", response.Headers.Location?.OriginalString);
	}

	[Fact]
	public async Task LoginPageShowsDemoCredentials()
	{
		using HttpClient client = factory.CreateClient();
		string html = await client.GetStringAsync("/");
		Assert.Contains("ssotest01", html, StringComparison.Ordinal);
		Assert.Contains("123456", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task AuthenticatedViewerPreservesOriginalEightStepPipelineAndManualStart()
	{
		using HttpClient client = await CreateSignedInClientAsync();
		string html = await client.GetStringAsync("/viewer");
		string javascript = await client.GetStringAsync("/js/viewer.js");

		Assert.Equal(8, Regex.Count(html, "class=\"pipeline-node pending\""));
		Assert.Contains("SSO Verified", html, StringComparison.Ordinal);
		Assert.Contains("Browser Handoff", html, StringComparison.Ordinal);
		Assert.Contains("Success", html, StringComparison.Ordinal);
		Assert.Contains("startButton.addEventListener('click', beginBrowserFlow)", javascript, StringComparison.Ordinal);
		Assert.DoesNotContain("keyboard-capture", html, StringComparison.Ordinal);
	}

	private async Task<HttpClient> CreateSignedInClientAsync()
	{
		HttpClient client = factory.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
		string loginHtml = await client.GetStringAsync("/");
		Match tokenMatch = Regex.Match(loginHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
		Assert.True(tokenMatch.Success);
		using FormUrlEncodedContent form = new(new Dictionary<string, string>
		{
			["username"] = "ssotest01",
			["password"] = "123456",
			["__RequestVerificationToken"] = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value)
		});
		using HttpResponseMessage response = await client.PostAsync("/sso/login", form);
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Equal("/viewer", response.Headers.Location?.OriginalString);
		return client;
	}
}
