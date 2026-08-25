using System;

namespace SsoGeminiLogin.Api.Services.Models;

public sealed record BrowserSession(string Id, string User, string AccountId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

