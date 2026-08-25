namespace SsoGeminiLogin.Api.Configuration;

public sealed class GatewayOptions
{
	public const string SectionName = "Gateway";

	public string SharedKey { get; set; } = string.Empty;
}

