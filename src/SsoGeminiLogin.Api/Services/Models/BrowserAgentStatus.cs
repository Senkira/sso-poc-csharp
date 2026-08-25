using System;
using System.Collections.Generic;

namespace SsoGeminiLogin.Api.Services.Models;

public sealed record BrowserAgentStatus(string Status, string? Message = null, string? Account = null, DateTimeOffset? CreatedAt = null, AgentViewport? Viewport = null, IReadOnlyList<AgentProgress>? Progress = null, bool AccountVerified = false);

