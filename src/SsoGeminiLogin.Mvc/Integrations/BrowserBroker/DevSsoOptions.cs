namespace SsoGeminiLogin.Mvc.Integrations.BrowserBroker;

public sealed class DevSsoOptions
{
	public const string SectionName = "DevSso";

	public bool Enabled { get; set; }

	public string Username { get; set; } = "ssotest01";

	public string Password { get; set; } = string.Empty;

	public string MappedUser { get; set; } = "codeassist.04@easybuy.co.th";
}

