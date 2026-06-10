using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Services;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

/// <summary>Anonymous email confirmation links for admin-face operators (v1.2 profile flow).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthEmailConfirmController : ControllerBase
{
	private readonly IAdminMeProfileService _profiles;

	public AuthEmailConfirmController(IAdminMeProfileService profiles) => _profiles = profiles;

	[HttpGet("confirm-email")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(MessageResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> ConfirmEmail([FromQuery] ConfirmEmailQuery query, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(query.UserId) || string.IsNullOrWhiteSpace(query.Token))
			return BadRequest(new ErrorResponseDto { Error = "Invalid confirmation link" });

		var result = await _profiles.ConfirmEmailAsync(query.UserId, query.Token, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new ErrorResponseDto { Error = result.Error ?? string.Empty });
		return Ok(new MessageResultDto { Message = "Email confirmed" });
	}
}
