using System;

namespace SsoGeminiLogin.Mvc.Integrations.Interfaces;

public sealed record BrowserAgentProgress(string Status, string Message, DateTimeOffset At);

