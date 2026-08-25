using System;
using System.Collections.Generic;

namespace SsoGeminiLogin.Api.Services.Models;

public sealed record BrowserSessionResult(string SessionId, string Status, string? Message, string? Account, DateTimeOffset? CreatedAt, AgentViewport? Viewport, IReadOnlyList<AgentProgress>? Progress, bool AccountVerified, bool? OpenAllowed = null);

