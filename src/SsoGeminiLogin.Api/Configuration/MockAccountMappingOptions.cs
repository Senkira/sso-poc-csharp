namespace SsoGeminiLogin.Api.Configuration;

public sealed class MockAccountMappingOptions
{
	public string SsoUser { get; set; } = string.Empty;

	public string AccountId { get; set; } = string.Empty;

	public string GeminiAccountEmail { get; set; } = string.Empty;

	public bool Enabled { get; set; } = true;
}

