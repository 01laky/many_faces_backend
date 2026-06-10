using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.Social;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Messenger;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IFaceModerationService _faceModeration;
	private readonly IConversationListService _conversations;

	public MessagesController(
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IFaceModerationService faceModeration,
		IConversationListService conversations)
	{
		_context = context;
		_faceScope = faceScope;
		_faceModeration = faceModeration;
		_conversations = conversations;
	}

	private async Task<bool> IsCallerFaceBannedInScopeAsync(CancellationToken cancellationToken) =>
		!string.IsNullOrEmpty(UserId) &&
		_faceScope.IsAvailable &&
		await _faceModeration.ShouldBlockPeerActivityInFaceAsync(UserId, _faceScope.FaceId, cancellationToken);

	/// <summary>GET /api/messages/conversations - Paginated conversation list (BE-RP3)</summary>
	[HttpGet("conversations")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> GetConversations(
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 50,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var result = await _conversations.GetConversationsAsync(UserId, page, pageSize, cancellationToken);
		var payload = result.Items.Select(c => new
		{
			otherUserId = c.OtherUserId,
			otherUserName = c.OtherUserName,
			otherUserEmail = c.OtherUserEmail,
			lastMessage = c.LastMessage,
			lastMessageAt = c.LastMessageAt,
			lastMessageFromMe = c.LastMessageFromMe,
			unreadCount = c.UnreadCount,
		}).ToList();

		return Ok(ListPaginationHelper.BuildEnvelope(payload, result.Page, result.PageSize, result.TotalCount, result.TotalPages));
	}

	/// <summary>GET /api/messages/requests - Message requests from non-friends</summary>
	[HttpGet("requests")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMessageRequests()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		// Get blocked user IDs (both directions)
		var blockedIds = await _context.UserBlocks
			.Where(b => b.BlockerId == UserId || b.BlockedId == UserId)
			.Select(b => b.BlockerId == UserId ? b.BlockedId : b.BlockerId)
			.ToListAsync();

		// Include removed: EF Core translates navigation-property access in Select directly.
		// Projecting the 3 needed Sender fields (not the full ApplicationUser) reduces row transfer.
		var rawRequests = await _context.Messages
			.AsNoTracking()
			.Where(m => m.ReceiverId == UserId && m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending)
			.Where(m => !blockedIds.Contains(m.SenderId))
			.GroupBy(m => m.SenderId)
			.Select(g => new
			{
				senderId = g.Key,
				senderFirstName = g.First().Sender.FirstName,
				senderLastName = g.First().Sender.LastName,
				senderEmail = g.First().Sender.Email,
				lastMessage = g.OrderByDescending(m => m.SentAt).First().Content,
				lastMessageAt = g.Max(m => m.SentAt),
				count = g.Count(),
			})
			.Take(200)
			.ToListAsync();

		var result = rawRequests.Select(r => new
		{
			senderId = r.senderId,
			senderName = (r.senderFirstName ?? "") + " " + (r.senderLastName ?? ""),
			senderEmail = r.senderEmail,
			lastMessage = r.lastMessage,
			lastMessageAt = r.lastMessageAt,
			count = r.count,
		}).ToList();

		return Ok(result);
	}

	/// <summary>GET /api/messages/with/{otherUserId} - Chat history with a user</summary>
	[HttpGet("with/{otherUserId}")]
	[ProducesResponseType(typeof(IEnumerable<MessageListItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMessages(string otherUserId, [FromQuery] MessageHistoryQuery query, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var callerFaceBanned = await IsCallerFaceBannedInScopeAsync(cancellationToken);
		var superAdminIds = await MessengerModerationHelper.GetSuperAdminUserIdsAsync(_context, new[] { otherUserId }, cancellationToken);
		if (MessengerModerationHelper.ShouldHidePeerConversation(callerFaceBanned, otherUserId, superAdminIds))
			return StatusCode(StatusCodes.Status403Forbidden, new CodedErrorResponseDto { Code = "face_banned", Error = "Account restricted in this community" });

		// Check if blocked
		var isBlocked = await _context.UserBlocks
			.AnyAsync(b =>
				(b.BlockerId == UserId && b.BlockedId == otherUserId) ||
				(b.BlockerId == otherUserId && b.BlockedId == UserId), cancellationToken);
		if (isBlocked)
			return Ok(Array.Empty<object>());

		var limit = query.Limit;
		var threadQuery = _context.Messages
			.Where(m =>
				((m.SenderId == UserId && m.ReceiverId == otherUserId) ||
				 (m.SenderId == otherUserId && m.ReceiverId == UserId)) &&
				(!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted));

		if (query.BeforeId is { } beforeId)
			threadQuery = threadQuery.Where(m => m.Id < beforeId);

		var messages = await threadQuery
			.Include(m => m.Sender).ThenInclude(u => u.UserRole)
			.OrderByDescending(m => m.Id)
			.Take(limit)
			.ToListAsync(cancellationToken);

		messages.Reverse();

		return Ok(messages.Select(m => new MessageListItemDto
		{
			Id = m.Id,
			SenderId = m.SenderId,
			SenderName = (m.Sender.FirstName ?? "") + " " + (m.Sender.LastName ?? ""),
			SenderGlobalRole = m.Sender.UserRole?.Name,
			IsPlatformAdministrator = OperatorModerationGuard.IsGlobalSuperAdminRole(m.Sender.UserRole?.Name),
			Content = m.Content,
			SentAt = m.SentAt,
			ReadAt = m.ReadAt,
		}));
	}

	/// <summary>POST /api/messages/with/{otherUserId}/read - Mark messages as read</summary>
	[HttpPost("with/{otherUserId}/read")]
	[ProducesResponseType(typeof(CountResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> MarkAsRead(string otherUserId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var toUpdate = await _context.Messages
			.Where(m => m.SenderId == otherUserId && m.ReceiverId == UserId && m.ReadAt == null)
			.ToListAsync();

		foreach (var m in toUpdate)
			m.ReadAt = DateTime.UtcNow;

		await _context.SaveChangesAsync();
		return Ok(new CountResultDto { Count = toUpdate.Count });
	}
}
