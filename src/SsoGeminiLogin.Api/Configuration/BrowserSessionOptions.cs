namespace SsoGeminiLogin.Api.Configuration;

public sealed class BrowserSessionOptions
{
	public const string SectionName = "BrowserSessions";

	public int TtlMinutes { get; set; } = 60;
}

