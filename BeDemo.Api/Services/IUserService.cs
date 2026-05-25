/*
 * IUserService.cs - Interface for User service operations
 * 
 * Defines contract for user business logic operations.
 * Separates business logic from controllers.
 */

using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// Interface for User service - defines contract for user operations
/// </summary>
public interface IUserService
{
	/// <summary>
	/// Get all users
	/// </summary>
	Task<IEnumerable<UserDto>> GetUsersAsync();

	/// <summary>
	/// Get user by ID
	/// </summary>
	Task<UserDto?> GetUserByIdAsync(string id);

	/// <summary>
	/// Create a new user
	/// </summary>
	Task<(UserDto? user, IEnumerable<string> errors)> CreateUserAsync(CreateUserDto dto);

	/// <summary>
	/// Update an existing user
	/// </summary>
	Task<(UserDto? user, IEnumerable<string> errors)> UpdateUserAsync(string id, UpdateUserDto dto);

	/// <summary>
	/// Delete a user by ID
	/// </summary>
	Task<bool> DeleteUserAsync(string id);
}
