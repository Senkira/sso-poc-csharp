using System;

namespace SsoGeminiLogin.Mvc.Models.ViewModels;

public sealed record PipelineEventViewModel(string Status, string Message, DateTimeOffset At);

