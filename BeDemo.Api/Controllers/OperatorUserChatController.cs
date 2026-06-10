using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.DTOs.OperatorUserChat;
using BeDemo.Api.Security;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin 1:1 user chat (per-operator threads).</summary>
// Backend-refactor X5/X6: the global SUPER_ADMIN gate (role-only, IsGlobalSuperAdmin) is enforced declaratively by
// the SuperAdmin policy instead of the per-action RequireSuperAdmin() check. Same matrix (anonymous → 401,
// non-super-admin → 403, super-admin → allowed); pinned by OperatorUserChatController tests.
[ApiController]
[Route("api/operator-user-chat")]
[Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]
public sealed class OperatorUserChatController : ApiControllerBase
{
	private readonly IOperatorUserChatService _chat;

	public OperatorUserChatController(IOperatorUserChatService chat)
	{
		_chat = chat;
	}

	/// <summary>Sidebar list for the logged-in super-admin (per-operator threads).</summary>
	[HttpGet("conversations")]
	public async Task<ActionResult<IReadOnlyList<OperatorUserChatConversationDto>>> ListConversations(
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var list = await _chat.ListConversationsAsync(UserId, cancellationToken);
		return Ok(list);
	}

	/// <summary>Paginated history newest-first; use <c>beforeId</c> for older pages.</summary>
	[HttpGet("with/{targetUserId}")]
	public async Task<ActionResult<OperatorUserChatHistoryPageDto>> GetHistory(
		string targetUserId,
		[FromQuery] OperatorUserChatHistoryQuery query,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var page = await _chat.GetHistoryAsync(UserId, targetUserId, query, cancellationToken);
		if (page == null)
			return NotFound(new { error = "User not found or invalid target" });
		return Ok(page);
	}

	[HttpGet("with/{targetUserId}/exists")]
	public async Task<ActionResult<OperatorUserChatThreadExistsDto>> GetExists(
		string targetUserId,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var dto = await _chat.GetThreadExistsAsync(UserId, targetUserId, cancellationToken);
		return Ok(dto);
	}

	[HttpPost("with/{targetUserId}/read")]
	public async Task<IActionResult> MarkRead(string targetUserId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var count = await _chat.MarkReadAsync(UserId, targetUserId, cancellationToken);
		return Ok(new { count });
	}
}
