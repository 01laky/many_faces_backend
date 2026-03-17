/*
 * OAuth2Controller.cs - Controller for OAuth2 authentication endpoints
 * 
 * This controller provides REST API endpoints for:
 * - POST /api/oauth2/token - get JWT access token and refresh token
 * - POST /api/oauth2/register - register new user
 * 
 * All endpoints use OAuth2Service for business logic.
 */

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Controller for OAuth2 authentication
/// </summary>
[ApiController]
[Route("api/oauth2")]  // All endpoints in this controller will have prefix /api/oauth2
public class OAuth2Controller : ControllerBase
{
    private readonly IOAuth2Service _oauth2Service;              // Service for OAuth2 operations
    private readonly UserManager<ApplicationUser> _userManager;   // ASP.NET Core Identity UserManager for user management
    private readonly ApplicationDbContext _context;               // DbContext for explicit save operations
    private readonly ILogger<OAuth2Controller> _logger;          // Logger for logging

    public OAuth2Controller(
        IOAuth2Service oauth2Service,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<OAuth2Controller> logger)
    {
        _oauth2Service = oauth2Service;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/oauth2/token
    /// 
    /// OAuth2 token endpoint - generates JWT access token and refresh token
    /// 
    /// Request body must contain:
    /// - grant_type: "password" or "refresh_token"
    /// - client_id: OAuth2 client ID
    /// - client_secret: OAuth2 client secret
    /// - username, password: For password grant type
    /// - refresh_token: For refresh_token grant type
    /// 
    /// Returns OAuth2TokenResponse with access token, refresh token and expiration
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] OAuth2TokenRequest request)
    {
        // Validates ModelState - checks if request satisfies data annotations (e.g., [Required])
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for token request");
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "Invalid request parameters"
            });
        }

        // Validates that grant_type is provided (it's a required parameter)
        if (string.IsNullOrEmpty(request.GrantType))
        {
            _logger.LogWarning("Token request missing grant_type");
            return BadRequest(new OAuth2ErrorResponse
            {
                Error = "invalid_request",
                ErrorDescription = "grant_type is required"
            });
        }

        // Validates that username/password don't contain null bytes (PostgreSQL doesn't support them)
        if (!string.IsNullOrEmpty(request.Username) && request.Username.Contains('\0'))
        {
            _logger.LogWarning("Username contains null byte");
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid credentials"
            });
        }

        if (!string.IsNullOrEmpty(request.Password) && request.Password.Contains('\0'))
        {
            _logger.LogWarning("Password contains null byte");
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid credentials"
            });
        }

        // Calls OAuth2Service to generate token
        // This method validates credentials and creates JWT token
        OAuth2TokenResponse? tokenResponse;
        try
        {
            tokenResponse = await _oauth2Service.GenerateTokenAsync(request, _userManager);
        }
        catch (PostgresException ex) when (ex.MessageText?.Contains("does not exist", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning(
                ex,
                "Database not initialized (PostgreSQL role/object missing). Login unavailable. SqlState: {SqlState}, Message: {Message}",
                ex.SqlState, ex.MessageText);
            return StatusCode(503, new OAuth2ErrorResponse
            {
                Error = "temporarily_unavailable",
                ErrorDescription = "Authentication service is not ready. Please try again later or contact support."
            });
        }
        catch (Exception ex) when (ex.InnerException is PostgresException inner && inner.MessageText?.Contains("does not exist", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning(
                ex.InnerException,
                "Database not initialized (PostgreSQL role/object missing, wrapped). Login unavailable. Message: {Message}",
                ((PostgresException)ex.InnerException).MessageText);
            return StatusCode(503, new OAuth2ErrorResponse
            {
                Error = "temporarily_unavailable",
                ErrorDescription = "Authentication service is not ready. Please try again later or contact support."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token endpoint failed. Returning 503 so client does not see Internal Server Error.");
            return StatusCode(503, new OAuth2ErrorResponse
            {
                Error = "temporarily_unavailable",
                ErrorDescription = "Authentication service is temporarily unavailable. Please try again later."
            });
        }

        // If token generation failed (e.g., incorrect credentials), returns 401 Unauthorized
        if (tokenResponse == null)
        {
            _logger.LogWarning("Token generation failed for grant type: {GrantType}", request.GrantType);
            return Unauthorized(new OAuth2ErrorResponse
            {
                Error = "invalid_grant",                              // OAuth2 error code
                ErrorDescription = "Invalid credentials or grant type" // Error description
            });
        }

        // If everything passed, returns 200 OK with token response
        _logger.LogInformation("Token generated successfully for grant type: {GrantType}", request.GrantType);
        return Ok(tokenResponse);
    }

    /// <summary>
    /// POST /api/oauth2/register
    /// 
    /// Register new user
    /// 
    /// Request body must contain:
    /// - email: Email address (also used as username)
    /// - password: Password (must satisfy Identity requirements)
    /// - firstName: Optional first name
    /// - lastName: Optional last name
    /// 
    /// Returns 200 OK with userId if registration passed, otherwise 400 Bad Request with errors
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] OAuth2RegisterModel model)
    {
        // Validates ModelState - checks data annotations
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for registration");
            return BadRequest(ModelState);
        }

        // Validates that email doesn't contain null bytes (PostgreSQL doesn't support them)
        if (!string.IsNullOrEmpty(model.Email) && model.Email.Contains('\0'))
        {
            _logger.LogWarning("Email contains null byte");
            return BadRequest(new { error = "Email cannot contain null bytes" });
        }

        // Validates that password doesn't contain null bytes
        if (!string.IsNullOrEmpty(model.Password) && model.Password.Contains('\0'))
        {
            _logger.LogWarning("Password contains null byte");
            return BadRequest(new { error = "Password cannot contain null bytes" });
        }

        // Get USER (global) role - default for new users
        var userRole = await _context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.User);
        if (userRole == null)
        {
            _logger.LogError("USER role not found. Please ensure UserRoles are seeded.");
            return BadRequest(new { error = "System configuration error: USER role not found" });
        }

        // Creates new ApplicationUser instance
        var user = new ApplicationUser
        {
            UserName = model.Email,      // Email is used as username
            Email = model.Email,          // Email address
            FirstName = model.FirstName, // Optional first name
            LastName = model.LastName,   // Optional last name
            UserRoleId = userRole.Id     // Assign default USER role
        };

        // Creates user in database using ASP.NET Core Identity
        // Identity automatically hashes password and validates requirements (length, characters, etc.)
        var result = await _userManager.CreateAsync(user, model.Password);

        // If registration succeeded
        if (result.Succeeded)
        {
            // Ensure user is fully persisted before creating UserProfile
            // This helps with test timing issues in in-memory databases
            await _context.SaveChangesAsync();

            // Create UserProfile automatically for new user (one-to-one relationship)
            var userProfile = new UserProfile
            {
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

            // Create UserFaceProfile and UserFaceRole (FACE_HOST) for each Face
            var faces = await _context.Faces.ToListAsync();
            var userFaceProfiles = faces.Select(face => new UserFaceProfile
            {
                UserProfileId = userProfile.Id,
                FaceId = face.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            if (userFaceProfiles.Any())
            {
                _context.UserFaceProfiles.AddRange(userFaceProfiles);
                var faceHostRole = await _context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost);
                if (faceHostRole != null)
                {
                    var userFaceRoles = faces.Select(face => new UserFaceRole
                    {
                        UserId = user.Id,
                        FaceId = face.Id,
                        UserRoleId = faceHostRole.Id,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();
                    _context.UserFaceRoles.AddRange(userFaceRoles);
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created {Count} UserFaceProfile(s) for user: {Email}", userFaceProfiles.Count, model.Email);
            }

            // Verify user can be found immediately (for test reliability)
            var verifyUser = await _userManager.FindByEmailAsync(model.Email);
            if (verifyUser == null)
            {
                // If user not found, wait a bit and try again (for in-memory DB timing)
                await Task.Delay(100);
                verifyUser = await _userManager.FindByEmailAsync(model.Email);
            }

            _logger.LogInformation("User registered successfully: {Email} with UserProfile ID: {ProfileId} and {FaceProfileCount} face profile(s)",
                model.Email, userProfile.Id, userFaceProfiles.Count);
            return Ok(new { message = "User registered successfully", userId = user.Id, profileId = userProfile.Id, faceProfileCount = userFaceProfiles.Count });
        }

        // If registration failed (e.g., email already exists, password doesn't meet requirements), returns errors
        _logger.LogWarning("User registration failed: {Email}, Errors: {Errors}", model.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
        return BadRequest(result.Errors);
    }
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
