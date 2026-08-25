namespace SsoGeminiLogin.Mvc.Integrations.BrowserBroker;

public sealed class BrowserBrokerOptions
{
	public const string SectionName = "BrokerApi";

	public string BaseUrl { get; set; } = "http://127.0.0.1:4174";

	public string GatewayKey { get; set; } = string.Empty;

	public int TimeoutSeconds { get; set; } = 120;

	public long MaximumResponseBytes { get; set; } = 1048576L;
}

