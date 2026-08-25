using System;
using System.Collections.Generic;

namespace SsoGeminiLogin.Mvc.Integrations.Interfaces;

public sealed record BrowserSessionState(string SessionId, string Status, string? Message, string? Account, DateTimeOffset? CreatedAt, BrowserViewport? Viewport, IReadOnlyList<BrowserAgentProgress> Progress, bool AccountVerified, bool CanOpen);

