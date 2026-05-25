/*
 * OAuth2Controller.cs - Controller for OAuth2 authentication endpoints
 * 
 * This controller provides REST API endpoints for:
 * - POST /api/oauth2/token - get JWT access token and refresh token
 * - POST /api/oauth2/register - register new user
 * 
 * Token logic is orchestrated by <see cref="Services.IOAuth2Service"/> (delegates to client validator, access JWT factory, refresh store).
 */

using Microsoft.AspNetCore.Identity;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Validation.OAuth;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Controller for OAuth2 authentication
/// </summary>
[ApiController]
[Route("api/oauth2")]  // All endpoints in this controller will have prefix /api/oauth2
[AllowAnonymous]
public class OAuth2Controller : ControllerBase
{
	private readonly IOAuth2Service _oauth2Service;              // Service for OAuth2 operations
	private readonly UserManager<ApplicationUser> _userManager;   // ASP.NET Core Identity UserManager for user management
	private readonly ApplicationDbContext _context;               // DbContext for explicit save operations
	private readonly ILogger<OAuth2Controller> _logger;          // Logger for logging
	private readonly IValidator<OAuth2TokenRequest> _tokenRequestValidator;

	public OAuth2Controller(
		IOAuth2Service oauth2Service,
		UserManager<ApplicationUser> userManager,
		ApplicationDbContext context,
		ILogger<OAuth2Controller> logger,
		IValidator<OAuth2TokenRequest> tokenRequestValidator)
	{
		_oauth2Service = oauth2Service;
		_userManager = userManager;
		_context = context;
		_logger = logger;
		_tokenRequestValidator = tokenRequestValidator;
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
	/// <summary>ACL A21: fixed-window rate limit per client IP (relaxed in <c>Testing</c> environment).</summary>
	[HttpPost("token")]
	[EnableRateLimiting("oauth-token")]
	public async Task<IActionResult> Token(
		[FromBody][CustomizeValidator(Skip = true)] OAuth2TokenRequest request,
		CancellationToken cancellationToken)
	{
		var validation = await _tokenRequestValidator.ValidateAsync(request, cancellationToken);
		if (!validation.IsValid)
		{
			var description = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
			return BadRequest(new OAuth2ErrorResponse
			{
				Error = "invalid_request",
				ErrorDescription = string.IsNullOrWhiteSpace(description) ? "Validation failed." : description,
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
	/// <summary>ACL A21: fixed-window rate limit per client IP (same bypass flag as token in Testing).</summary>
	[HttpPost("register")]
	[EnableRateLimiting("oauth-register")]
	[Obsolete("Use POST /api/oauth2/register/request then /api/oauth2/register/complete")]
	public IActionResult Register([FromBody] OAuth2RegisterModel model)
	{
		_ = model;
		return BadRequest(new
		{
			error = "registration_flow_deprecated",
			message = "Use POST /api/oauth2/register/request (email) then complete signup from the email link with POST /api/oauth2/register/complete.",
		});
	}
}
