using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.OAuth;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Public email-code registration under <c>/api/oauth2/register/*</c> (face-prefix exempt, same as legacy register).
/// </summary>
[ApiController]
[Route("api/oauth2/register")]
[AllowAnonymous]
public sealed class OAuth2RegistrationController : ControllerBase
{
	private readonly IRegistrationInviteService _invites;
	private readonly ILogger<OAuth2RegistrationController> _logger;

	public OAuth2RegistrationController(IRegistrationInviteService invites, ILogger<OAuth2RegistrationController> logger)
	{
		_invites = invites;
		_logger = logger;
	}

	/// <summary>Step 1: create/replace pending invite and queue registration email.</summary>
	[HttpPost("request")]
	[EnableRateLimiting("oauth-register")]
	public async Task<IActionResult> RequestSignup([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		if (ContainsNullByte(dto.Email))
		{
			return BadRequest(new { error = "Email cannot contain null bytes" });
		}

		var response = await _invites.RequestAsync(dto, cancellationToken).ConfigureAwait(false);
		return Ok(response);
	}

	/// <summary>Rotate hash+code and resend mail for an existing pending invite.</summary>
	[HttpPost("resend")]
	[EnableRateLimiting("oauth-register")]
	public async Task<IActionResult> Resend([FromBody] RegisterResendDto dto, CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		var response = await _invites.ResendAsync(dto, cancellationToken).ConfigureAwait(false);
		return Ok(response);
	}

	/// <summary>Read-only prefill for complete-registration UI; never returns the verification code.</summary>
	[HttpGet("prefill")]
	[EnableRateLimiting("oauth-register-prefill")]
	public async Task<IActionResult> Prefill([FromQuery] RegisterPrefillQuery query, CancellationToken cancellationToken)
	{
		var prefill = await _invites.GetPrefillAsync(query.Hash!, cancellationToken).ConfigureAwait(false);
		if (prefill == null)
		{
			return BadRequest(new { error = "Invalid request" });
		}

		return Ok(prefill);
	}

	/// <summary>Step 2: verify hash+code, create user, return OAuth2 tokens (auto-login).</summary>
	[HttpPost("complete")]
	[EnableRateLimiting("oauth-register")]
	public async Task<IActionResult> Complete([FromBody] RegisterCompleteDto dto, CancellationToken cancellationToken)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		if (ContainsNullByte(dto.Password) || ContainsNullByte(dto.Hash))
		{
			return BadRequest(new { error = "Invalid characters in request" });
		}

		var result = await _invites.CompleteAsync(dto, cancellationToken).ConfigureAwait(false);
		if (result == null)
		{
			_logger.LogWarning("Registration complete failed for hash (generic)");
			return BadRequest(new { error = "invalid_invite", message = "Invalid or expired registration. Request a new code." });
		}

		return Ok(result);
	}

	private static bool ContainsNullByte(string? value) =>
		!string.IsNullOrEmpty(value) && value.Contains('\0');
}
