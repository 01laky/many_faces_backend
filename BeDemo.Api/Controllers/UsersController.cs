using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;
using BeDemo.Api.Data;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UsersController> _logger;
    private readonly ApplicationDbContext _context;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        ILogger<UsersController> logger,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// GET /api/users
    /// Get list of all users
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = _userManager.Users.ToList().AsEnumerable();

            var userDtos = users.Select(u => new
            {
                id = u.Id,
                email = u.Email,
                firstName = u.FirstName,
                lastName = u.LastName,
                createdAt = u.CreatedAt,
            }).ToList();

            _logger.LogInformation("Retrieved {Count} users", userDtos.Count);
            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { error = "An error occurred while retrieving users" });
        }
    }

    /// <summary>
    /// GET /api/users/{id}
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", id);
                return NotFound(new { error = "User not found" });
            }

            var userDto = new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                createdAt = user.CreatedAt,
            };

            _logger.LogInformation("Retrieved user: {UserId}", id);
            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user: {UserId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving user" });
        }
    }

    /// <summary>
    /// POST /api/users
    /// Create a new user
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Get USER role (default role for new users)
            var userRole = await _context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.RoleNames.User);
            if (userRole == null)
            {
                _logger.LogError("USER role not found. Please ensure UserRoles are seeded.");
                return StatusCode(500, new { error = "System configuration error: USER role not found" });
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                UserRoleId = userRole.Id // Assign default USER role
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                var userDto = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    createdAt = user.CreatedAt,
                };

                _logger.LogInformation("User created: {UserId}", user.Id);
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
            }

            _logger.LogWarning("User creation failed: {Email}, Errors: {Errors}", model.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(result.Errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { error = "An error occurred while creating user" });
        }
    }

    /// <summary>
    /// PUT /api/users/{id}
    /// Update user by ID
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                _logger.LogWarning("User not found for update: {UserId}", id);
                return NotFound(new { error = "User not found" });
            }

            // Update user properties
            if (model.Email != null)
            {
                user.Email = model.Email;
                user.UserName = model.Email;
            }
            if (model.FirstName != null)
            {
                user.FirstName = model.FirstName;
            }
            if (model.LastName != null)
            {
                user.LastName = model.LastName;
            }

            // Update password if provided
            IdentityResult result;
            if (!string.IsNullOrEmpty(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                result = await _userManager.ResetPasswordAsync(user, token, model.Password);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Password update failed for user: {UserId}, Errors: {Errors}", id, string.Join(", ", result.Errors.Select(e => e.Description)));
                    return BadRequest(result.Errors);
                }
            }

            result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var userDto = new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    createdAt = user.CreatedAt,
                };

                _logger.LogInformation("User updated: {UserId}", id);
                return Ok(userDto);
            }

            _logger.LogWarning("User update failed: {UserId}, Errors: {Errors}", id, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(result.Errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user: {UserId}", id);
            return StatusCode(500, new { error = "An error occurred while updating user" });
        }
    }
}

/// <summary>
/// Model for creating a new user
/// </summary>
public class CreateUserModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(4, ErrorMessage = "Password must be at least 4 characters")]
    public string Password { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}

/// <summary>
/// Model for updating a user
/// </summary>
public class UpdateUserModel
{
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string? Email { get; set; }

    [MinLength(4, ErrorMessage = "Password must be at least 4 characters")]
    public string? Password { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
