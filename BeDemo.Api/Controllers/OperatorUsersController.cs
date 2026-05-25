using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.DTOs.OperatorUsers;
using BeDemo.Api.Models.Requests.OperatorUsers;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin operator user moderation (detail, bans, face roles, platform messages).</summary>
[ApiController]
[Route("api/operator-users")]
[Authorize]
public sealed class OperatorUsersController : ControllerBase
{
	private readonly IAccessEvaluator _access;
	private readonly IOperatorUserModerationService _moderation;

	public OperatorUsersController(IAccessEvaluator access, IOperatorUserModerationService moderation)
	{
		_access = access;
		_moderation = moderation;
	}

	private string? OperatorUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

	private bool RequireSuperAdmin() => _access.IsGlobalSuperAdmin(User);

	[HttpGet("users/{id}/detail")]
	public async Task<ActionResult<OperatorUserDetailDto>> GetDetail(string id, CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		var dto = await _moderation.GetDetailAsync(id, cancellationToken);
		if (dto == null)
			return NotFound(new { error = "User not found" });
		return Ok(dto);
	}

	[HttpPatch("users/{id}/faces/{faceId}/role")]
	public async Task<IActionResult> SetFaceRole(
		string id,
		int faceId,
		[FromBody] OperatorSetFaceRoleRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();
		var result = await _moderation.SetFaceRoleAsync(
			OperatorUserId, id, faceId, request.UserRoleId, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return Ok(new { userRoleId = request.UserRoleId });
	}

	[HttpPost("users/{id}/global-ban")]
	public async Task<IActionResult> GlobalBan(
		string id,
		[FromBody] OperatorBanReasonRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();
		var result = await _moderation.GlobalBanAsync(
			OperatorUserId, id, request.Reason, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return Ok(new { banned = true, alreadyBanned = result.AlreadyBanned });
	}

	[HttpDelete("users/{id}/global-ban")]
	public async Task<IActionResult> GlobalUnban(string id, CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();
		var result = await _moderation.GlobalUnbanAsync(
			OperatorUserId, id, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return result.StatusCode == StatusCodes.Status204NoContent ? NoContent() : Ok(new { banned = false });
	}

	[HttpPost("users/{id}/faces/{faceId}/ban")]
	public async Task<IActionResult> FaceBan(
		string id,
		int faceId,
		[FromBody] OperatorBanReasonRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();
		var result = await _moderation.FaceBanAsync(
			OperatorUserId, id, faceId, request.Reason, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return Ok(new { faceBanned = true, alreadyBanned = result.AlreadyBanned });
	}

	[HttpDelete("users/{id}/faces/{faceId}/ban")]
	public async Task<IActionResult> FaceUnban(string id, int faceId, CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();
		var result = await _moderation.FaceUnbanAsync(
			OperatorUserId, id, faceId, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return result.StatusCode == StatusCodes.Status204NoContent ? NoContent() : Ok(new { faceBanned = false });
	}

	[HttpPost("users/{id}/platform-messages")]
	public async Task<IActionResult> SendPlatformMessage(
		string id,
		[FromBody] OperatorPlatformMessageRequest request,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();
		var result = await _moderation.SendPlatformMessageAsync(
			OperatorUserId, id, request.Content, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new { error = result.Error });
		return Ok(new { messageId = result.MessageId });
	}
}
