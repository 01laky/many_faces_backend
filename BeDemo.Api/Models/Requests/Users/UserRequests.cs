namespace BeDemo.Api.Models.Requests.Users;

/// <summary>
/// Model for creating a new user
/// </summary>
public class CreateUserModel
{
	public string Email { get; set; } = string.Empty;

	public string Password { get; set; } = string.Empty;

	public string? FirstName { get; set; }

	public string? LastName { get; set; }
}

/// <summary>
/// Model for updating a user
/// </summary>
public class UpdateUserModel
{
	public string? Email { get; set; }

	public string? Password { get; set; }

	public string? FirstName { get; set; }

	public string? LastName { get; set; }
}

