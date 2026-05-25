using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BeDemo.Api.Models.DTOs.OperatorUserChat;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>Super-admin 1:1 user chat (per-operator threads).</summary>
[ApiController]
[Route("api/operator-user-chat")]
[Authorize]
public sealed class OperatorUserChatController : ControllerBase
{
	private readonly IAccessEvaluator _access;
	private readonly IOperatorUserChatService _chat;

	public OperatorUserChatController(IAccessEvaluator access, IOperatorUserChatService chat)
	{
		_access = access;
		_chat = chat;
	}

	private string? OperatorUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

	/// <summary>Sidebar list for the logged-in super-admin (per-operator threads).</summary>
	[HttpGet("conversations")]
	public async Task<ActionResult<IReadOnlyList<OperatorUserChatConversationDto>>> ListConversations(
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var list = await _chat.ListConversationsAsync(OperatorUserId, cancellationToken);
		return Ok(list);
	}

	/// <summary>Paginated history newest-first; use <c>beforeId</c> for older pages.</summary>
	[HttpGet("with/{targetUserId}")]
	public async Task<ActionResult<OperatorUserChatHistoryPageDto>> GetHistory(
		string targetUserId,
		[FromQuery] OperatorUserChatHistoryQuery query,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var page = await _chat.GetHistoryAsync(OperatorUserId, targetUserId, query, cancellationToken);
		if (page == null)
			return NotFound(new { error = "User not found or invalid target" });
		return Ok(page);
	}

	[HttpGet("with/{targetUserId}/exists")]
	public async Task<ActionResult<OperatorUserChatThreadExistsDto>> GetExists(
		string targetUserId,
		CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var dto = await _chat.GetThreadExistsAsync(OperatorUserId, targetUserId, cancellationToken);
		return Ok(dto);
	}

	[HttpPost("with/{targetUserId}/read")]
	public async Task<IActionResult> MarkRead(string targetUserId, CancellationToken cancellationToken)
	{
		if (!RequireSuperAdmin())
			return Forbid();
		if (string.IsNullOrEmpty(OperatorUserId))
			return Unauthorized();

		var count = await _chat.MarkReadAsync(OperatorUserId, targetUserId, cancellationToken);
		return Ok(new { count });
	}

	private bool RequireSuperAdmin() => _access.IsGlobalSuperAdmin(User);
}
