/*
 * UserService.cs - Service for User business logic operations
 * 
 * This service handles all user-related business logic.
 * Separates business logic from controllers for better testability and maintainability.
 */

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// User service implementation - handles user business logic
/// </summary>
public class UserService : IUserService
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<UserService> _logger;

	public UserService(
		UserManager<ApplicationUser> userManager,
		ILogger<UserService> logger)
	{
		_userManager = userManager;
		_logger = logger;
	}

	/// <summary>
	/// Get all users
	/// </summary>
	public async Task<IEnumerable<UserDto>> GetUsersAsync()
	{
		try
		{
			var users = _userManager.Users.ToList();

			var userDtos = users.Select(u => new UserDto
			{
				Id = u.Id,
				Email = u.Email ?? string.Empty,
				FirstName = u.FirstName,
				LastName = u.LastName,
				CreatedAt = u.CreatedAt
			}).ToList();

			_logger.LogInformation("Retrieved {Count} users", userDtos.Count);
			return userDtos;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving users");
			throw;
		}
	}

	/// <summary>
	/// Get user by ID
	/// </summary>
	public async Task<UserDto?> GetUserByIdAsync(string id)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(id);

			if (user == null)
			{
				_logger.LogWarning("User not found: {UserId}", id);
				return null;
			}

			var userDto = new UserDto
			{
				Id = user.Id,
				Email = user.Email ?? string.Empty,
				FirstName = user.FirstName,
				LastName = user.LastName,
				CreatedAt = user.CreatedAt
			};

			_logger.LogInformation("Retrieved user: {UserId}", id);
			return userDto;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving user: {UserId}", id);
			throw;
		}
	}

	/// <summary>
	/// Create a new user
	/// </summary>
	public async Task<(UserDto? user, IEnumerable<string> errors)> CreateUserAsync(CreateUserDto dto)
	{
		try
		{
			var user = new ApplicationUser
			{
				UserName = dto.Email,
				Email = dto.Email,
				FirstName = dto.FirstName,
				LastName = dto.LastName,
			};

			var result = await _userManager.CreateAsync(user, dto.Password);

			if (result.Succeeded)
			{
				var userDto = new UserDto
				{
					Id = user.Id,
					Email = user.Email ?? string.Empty,
					FirstName = user.FirstName,
					LastName = user.LastName,
					CreatedAt = user.CreatedAt
				};

				_logger.LogInformation("User created: {UserId}", user.Id);
				return (userDto, Enumerable.Empty<string>());
			}

			var errors = result.Errors.Select(e => e.Description);
			_logger.LogWarning("User creation failed: {Email}, Errors: {Errors}", dto.Email, string.Join(", ", errors));
			return (null, errors);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating user");
			throw;
		}
	}

	/// <summary>
	/// Update an existing user
	/// </summary>
	public async Task<(UserDto? user, IEnumerable<string> errors)> UpdateUserAsync(string id, UpdateUserDto dto)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(id);

			if (user == null)
			{
				_logger.LogWarning("User not found for update: {UserId}", id);
				return (null, new[] { "User not found" });
			}

			// Update user properties
			if (dto.Email != null)
			{
				user.Email = dto.Email;
				user.UserName = dto.Email;
			}
			if (dto.FirstName != null)
			{
				user.FirstName = dto.FirstName;
			}
			if (dto.LastName != null)
			{
				user.LastName = dto.LastName;
			}

			// Update password if provided
			IdentityResult result;
			if (!string.IsNullOrEmpty(dto.Password))
			{
				var token = await _userManager.GeneratePasswordResetTokenAsync(user);
				result = await _userManager.ResetPasswordAsync(user, token, dto.Password);

				if (!result.Succeeded)
				{
					var errors = result.Errors.Select(e => e.Description);
					_logger.LogWarning("Password update failed for user: {UserId}, Errors: {Errors}", id, string.Join(", ", errors));
					return (null, errors);
				}
			}

			result = await _userManager.UpdateAsync(user);

			if (result.Succeeded)
			{
				var userDto = new UserDto
				{
					Id = user.Id,
					Email = user.Email ?? string.Empty,
					FirstName = user.FirstName,
					LastName = user.LastName,
					CreatedAt = user.CreatedAt
				};

				_logger.LogInformation("User updated: {UserId}", id);
				return (userDto, Enumerable.Empty<string>());
			}

			var updateErrors = result.Errors.Select(e => e.Description);
			_logger.LogWarning("User update failed: {UserId}, Errors: {Errors}", id, string.Join(", ", updateErrors));
			return (null, updateErrors);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating user: {UserId}", id);
			throw;
		}
	}

	/// <summary>
	/// Delete a user by ID
	/// </summary>
	public async Task<bool> DeleteUserAsync(string id)
	{
		try
		{
			var user = await _userManager.FindByIdAsync(id);

			if (user == null)
			{
				_logger.LogWarning("User not found for deletion: {UserId}", id);
				return false;
			}

			var result = await _userManager.DeleteAsync(user);

			if (result.Succeeded)
			{
				_logger.LogInformation("User deleted: {UserId}", id);
				return true;
			}

			_logger.LogWarning("User deletion failed: {UserId}, Errors: {Errors}", id, string.Join(", ", result.Errors.Select(e => e.Description)));
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting user: {UserId}", id);
			throw;
		}
	}
}
