using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SsoGeminiLogin.Api.Models.Responses;

public sealed record BrowserSessionResponse(string SessionId, string Status, string? Message, string? Account, DateTimeOffset? CreatedAt, AgentViewportResponse? Viewport, IReadOnlyList<AgentProgressResponse>? Progress, bool AccountVerified, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? OpenAllowed = null);

