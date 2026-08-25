namespace SsoGeminiLogin.Api.Models.Responses;

public sealed record BrowserSessionCreatedResponse(string SessionId, string Status, string StatusUrl);

