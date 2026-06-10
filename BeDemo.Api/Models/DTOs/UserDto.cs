/*
 * UserDto.cs - Data Transfer Objects (DTOs) for User operations
 * 
 * This file contains DTOs used for User API communication.
 * DTOs separate API contracts from domain entities.
 */

using System.ComponentModel.DataAnnotations;
using BeDemo.Api.Configuration;

namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// User DTO - represents user data returned from API
/// </summary>
public class UserDto
{
	public string Id { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create User DTO - used when creating a new user
/// </summary>
public class CreateUserDto
{
	[Required(ErrorMessage = "Email is required")]
	[EmailAddress(ErrorMessage = "Invalid email address")]
	public string Email { get; set; } = string.Empty;

	[Required(ErrorMessage = "Password is required")]
	[MinLength(IdentityPasswordPolicyOptions.RecommendedMinimumLength, ErrorMessage = "Password must be at least 12 characters")]
	public string Password { get; set; } = string.Empty;

	public string? FirstName { get; set; }

	public string? LastName { get; set; }
}

/// <summary>Paginated user list envelope returned by GET /api/users.</summary>
public sealed class UserListEnvelopeDto
{
	public IEnumerable<UserDto> Items { get; init; } = [];
	public int TotalCount { get; init; }
	public int Page { get; init; }
	public int PageSize { get; init; }
	public int TotalPages { get; init; }
}

/// <summary>Single message item in the thread messages list.</summary>
public sealed class MessageListItemDto
{
	public int Id { get; init; }
	public string SenderId { get; init; } = string.Empty;
	public string SenderName { get; init; } = string.Empty;
	public string? SenderGlobalRole { get; init; }
	public bool IsPlatformAdministrator { get; init; }
	public string Content { get; init; } = string.Empty;
	public DateTime SentAt { get; init; }
	public DateTime? ReadAt { get; init; }
}

/// <summary>
/// Update User DTO - used when updating an existing user
/// </summary>
public class UpdateUserDto
{
	[EmailAddress(ErrorMessage = "Invalid email address")]
	public string? Email { get; set; }

	[MinLength(IdentityPasswordPolicyOptions.RecommendedMinimumLength, ErrorMessage = "Password must be at least 12 characters")]
	public string? Password { get; set; }

	public string? FirstName { get; set; }

	public string? LastName { get; set; }
}
