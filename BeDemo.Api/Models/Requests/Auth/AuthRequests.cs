namespace BeDemo.Api.Models.Requests.Auth;

public class RegisterModel
{
	public string Email { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
}

public class LoginModel
{
	public string Email { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public bool RememberMe { get; set; }
}

/// <summary>
/// Model for user registration
/// </summary>
public class OAuth2RegisterModel
{
	public string Email { get; set; } = string.Empty;      // Email address (required)
	public string Password { get; set; } = string.Empty;    // Password (required)
	public string? FirstName { get; set; }                 // First name (optional)
	public string? LastName { get; set; }                 // Last name (optional)
}

