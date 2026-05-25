using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Models.Requests.OperatorUsers;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin self-service profile on the admin face (identity, password, face roles).</summary>
[ApiController]
[Route("api/admin/me")]
[Authorize]
public sealed class AdminMeProfileController : ControllerBase
{
	private readonly IAccessEvaluator _access;
	private readonly IAdminMeProfileService _profiles;

	public AdminMeProfileController(IAccessEvaluator access, IAdminMeProfileService profiles)
	{
		_access = access;
		_profiles = profiles;
	}

	private string? CallerUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

	private bool RequireSuperAdmin() => _access.IsGlobalSuperAdmin(User);

	[HttpGet("profile")]
	public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(CallerUserId))
			return Unauthorized();

		var dto = await _profiles.GetProfileAsync(
			CallerUserId,
			Request.Scheme,
			Request.Host.Value!,
			cancellationToken);
		if (dto == null)
			return NotFound(new { error = "User not found" });
		return Ok(dto);
	}

	[HttpPut("profile")]
	public async Task<IActionResult> UpdateProfile(
		[FromBody] UpdateAdminMeProfileRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(CallerUserId))
			return Unauthorized();

		var locale = System.Globalization.CultureInfo.CurrentUICulture.Name;
		var result = await _profiles.UpdateProfileAsync(
			CallerUserId,
			request,
			Request.Scheme,
			Request.Host.Value!,
			locale,
			HttpContext.TraceIdentifier,
			cancellationToken);

		if (result.Profile == null)
			return StatusCode(result.StatusCode, new { error = result.Error });

		return Ok(result.Profile);
	}

	[HttpPut("password")]
	public async Task<IActionResult> UpdatePassword(
		[FromBody] UpdateAdminMePasswordRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(CallerUserId))
			return Unauthorized();

		var result = await _profiles.UpdatePasswordAsync(CallerUserId, request, cancellationToken);
		if (result.Error != null)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return NoContent();
	}

	[HttpPatch("faces/{faceId:int}/role")]
	public async Task<IActionResult> PatchFaceRole(
		int faceId,
		[FromBody] OperatorSetFaceRoleRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(CallerUserId))
			return Unauthorized();

		var result = await _profiles.SetSelfFaceRoleAsync(
			CallerUserId,
			faceId,
			request.UserRoleId,
			HttpContext.TraceIdentifier,
			cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return Ok(new { userRoleId = request.UserRoleId });
	}

	[HttpPost("resend-email-confirmation")]
	public async Task<IActionResult> ResendEmailConfirmation(CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(CallerUserId))
			return Unauthorized();

		var locale = System.Globalization.CultureInfo.CurrentUICulture.Name;
		var result = await _profiles.ResendEmailConfirmationAsync(
			CallerUserId,
			Request.Scheme,
			Request.Host.Value!,
			locale,
			cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return Ok(new { message = "Confirmation email sent" });
	}
}
