using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Models.Requests.OperatorUsers;
using BeDemo.Api.Security;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin self-service profile on the admin face (identity, password, face roles).</summary>
// Backend-refactor X5/X6: the global SUPER_ADMIN gate (role-only, IsGlobalSuperAdmin) is enforced declaratively by
// the SuperAdmin policy instead of the per-action RequireSuperAdmin() check. Same matrix (anonymous → 401,
// non-super-admin → 403, super-admin → allowed); pinned by AdminMeProfileController tests.
[ApiController]
[Route("api/admin/me")]
[Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
public sealed class AdminMeProfileController : ApiControllerBase
{
	private readonly IAdminMeProfileService _profiles;

	public AdminMeProfileController(IAdminMeProfileService profiles)
	{
		_profiles = profiles;
	}

	[HttpGet("profile")]
	public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var dto = await _profiles.GetProfileAsync(
			UserId,
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
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var locale = System.Globalization.CultureInfo.CurrentUICulture.Name;
		var result = await _profiles.UpdateProfileAsync(
			UserId,
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
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var result = await _profiles.UpdatePasswordAsync(UserId, request, cancellationToken);
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
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var result = await _profiles.SetSelfFaceRoleAsync(
			UserId,
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
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var locale = System.Globalization.CultureInfo.CurrentUICulture.Name;
		var result = await _profiles.ResendEmailConfirmationAsync(
			UserId,
			Request.Scheme,
			Request.Host.Value!,
			locale,
			cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return Ok(new { message = "Confirmation email sent" });
	}
}
