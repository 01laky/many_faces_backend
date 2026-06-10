using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.DTOs.OperatorUsers;
using BeDemo.Api.Models.Requests.OperatorUsers;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin operator user moderation (detail, bans, face roles, platform messages).</summary>
// Backend-refactor X5/X6: the global SUPER_ADMIN gate (role-only, IsGlobalSuperAdmin) is enforced declaratively by
// the SuperAdmin policy instead of the per-action RequireSuperAdmin() check. Same matrix (anonymous → 401,
// non-super-admin → 403, super-admin → allowed); pinned by OperatorUsersController tests.
[ApiController]
[Route("api/operator-users")]
[Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
public sealed class OperatorUsersController : ApiControllerBase
{
	private readonly IOperatorUserModerationService _moderation;

	public OperatorUsersController(IOperatorUserModerationService moderation)
	{
		_moderation = moderation;
	}

	[HttpGet("users/{id}/detail")]
	[ProducesResponseType(typeof(OperatorUserDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<ActionResult<OperatorUserDetailDto>> GetDetail(string id, CancellationToken cancellationToken)
	{
		var dto = await _moderation.GetDetailAsync(id, cancellationToken);
		if (dto == null)
			return NotFound(new ErrorResponseDto { Error = "User not found" });
		return Ok(dto);
	}

	[HttpPatch("users/{id}/faces/{faceId}/role")]
	[ProducesResponseType(typeof(UserRoleIdResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> SetFaceRole(
		string id,
		int faceId,
		[FromBody] OperatorSetFaceRoleRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		var result = await _moderation.SetFaceRoleAsync(
			UserId, id, faceId, request.UserRoleId, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new ErrorResponseDto { Error = result.Error ?? string.Empty });
		return Ok(new UserRoleIdResultDto { UserRoleId = request.UserRoleId });
	}

	[HttpPost("users/{id}/global-ban")]
	[ProducesResponseType(typeof(BanResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GlobalBan(
		string id,
		[FromBody] OperatorBanReasonRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		var result = await _moderation.GlobalBanAsync(
			UserId, id, request.Reason, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new ErrorResponseDto { Error = result.Error ?? string.Empty });
		return Ok(new BanResultDto { Banned = true, AlreadyBanned = result.AlreadyBanned });
	}

	[HttpDelete("users/{id}/global-ban")]
	[ProducesResponseType(typeof(BanResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> GlobalUnban(string id, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		var result = await _moderation.GlobalUnbanAsync(
			UserId, id, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new ErrorResponseDto { Error = result.Error ?? string.Empty });
		return result.StatusCode == StatusCodes.Status204NoContent ? NoContent() : Ok(new BanResultDto { Banned = false });
	}

	[HttpPost("users/{id}/faces/{faceId}/ban")]
	[ProducesResponseType(typeof(FaceBanResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> FaceBan(
		string id,
		int faceId,
		[FromBody] OperatorBanReasonRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		var result = await _moderation.FaceBanAsync(
			UserId, id, faceId, request.Reason, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new ErrorResponseDto { Error = result.Error ?? string.Empty });
		return Ok(new FaceBanResultDto { FaceBanned = true, AlreadyBanned = result.AlreadyBanned });
	}

	[HttpDelete("users/{id}/faces/{faceId}/ban")]
	[ProducesResponseType(typeof(FaceBanResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> FaceUnban(string id, int faceId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		var result = await _moderation.FaceUnbanAsync(
			UserId, id, faceId, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new ErrorResponseDto { Error = result.Error ?? string.Empty });
		return result.StatusCode == StatusCodes.Status204NoContent ? NoContent() : Ok(new FaceBanResultDto { FaceBanned = false });
	}

	[HttpPost("users/{id}/platform-messages")]
	[ProducesResponseType(typeof(MessageIdResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> SendPlatformMessage(
		string id,
		[FromBody] OperatorPlatformMessageRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		var result = await _moderation.SendPlatformMessageAsync(
			UserId, id, request.Content, HttpContext.TraceIdentifier, cancellationToken);
		if (!result.Success)
			return StatusCode(result.StatusCode, new ErrorResponseDto { Error = result.Error ?? string.Empty });
		return Ok(new MessageIdResultDto { MessageId = result.MessageId });
	}
}
