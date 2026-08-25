using System;

namespace SsoGeminiLogin.Api.Models.Responses;

public sealed record AgentProgressResponse(string Status, string Message, DateTimeOffset At);

