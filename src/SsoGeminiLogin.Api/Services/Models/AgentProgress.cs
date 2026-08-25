using System;

namespace SsoGeminiLogin.Api.Services.Models;

public sealed record AgentProgress(string Status, string Message, DateTimeOffset At);

