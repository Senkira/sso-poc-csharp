using System;
using System.Collections.Generic;

namespace SsoGeminiLogin.Mvc.Models.ViewModels;

public sealed class ViewerViewModel
{
	public required string SsoUser { get; init; }

	public required string AccountEmail { get; init; }

	public string? SessionId { get; init; }

	public string Status { get; init; } = "not-started";

	public string StatusMessage { get; init; } = "Secure Browser has not started.";

	public IReadOnlyList<PipelineEventViewModel> Progress { get; init; } = Array.Empty<PipelineEventViewModel>();

	public bool CanStart { get; init; }

	public bool CanOpen { get; init; }

	public bool ShouldRefresh { get; init; }

	public bool WasStopped { get; init; }
}

