using System.ComponentModel.DataAnnotations;

namespace SsoGeminiLogin.Mvc.Models.ViewModels;

public sealed class LoginViewModel
{
	[Required]
	[StringLength(100, MinimumLength = 1)]
	public string Username { get; set; } = string.Empty;

	[Required]
	[StringLength(256, MinimumLength = 1)]
	[DataType(DataType.Password)]
	public string Password { get; set; } = string.Empty;

	public bool InvalidCredentials { get; set; }
}

