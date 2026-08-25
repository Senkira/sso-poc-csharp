namespace SsoGeminiLogin.Api.Configuration;

public sealed class AgentOptions
{
	public const string SectionName = "Agent";

	public string Mode { get; set; } = "Local";

	public string? ExecutablePath { get; set; }

	public string CredentialTarget { get; set; } = "ESB.GeminiBroker.CodeAssist04";

	public string? EdgeExecutable { get; set; }

	public string ProfileRoot { get; set; } = "data/profiles";

	public string ExpectedAccountId { get; set; } = "7c79d5435d249ca9";

	public string ExpectedAccountEmail { get; set; } = "codeassist.04@easybuy.co.th";

	public int MaxWorkers { get; set; } = 3;

	public bool Headless { get; set; } = true;
}

