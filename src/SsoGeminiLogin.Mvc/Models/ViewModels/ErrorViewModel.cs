namespace SsoGeminiLogin.Mvc.Models.ViewModels;

public sealed class ErrorViewModel
{
	public required string TraceId { get; init; }

	public int StatusCode { get; init; }

	public string Message { get; init; } = "The request could not be completed.";
}

