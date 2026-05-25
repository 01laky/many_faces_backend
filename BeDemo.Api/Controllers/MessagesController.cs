using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Social;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IFaceModerationService _faceModeration;

	public MessagesController(
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IFaceModerationService faceModeration)
	{
		_context = context;
		_faceScope = faceScope;
		_faceModeration = faceModeration;
	}

	private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

	private async Task<bool> IsCallerFaceBannedInScopeAsync(CancellationToken cancellationToken) =>
		!string.IsNullOrEmpty(UserId) &&
		_faceScope.IsAvailable &&
		await _faceModeration.ShouldBlockPeerActivityInFaceAsync(UserId, _faceScope.FaceId, cancellationToken);

	/// <summary>GET /api/messages/conversations - List conversations with last message</summary>
	[HttpGet("conversations")]
	public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		// Get blocked user IDs (both directions)
		var blockedIds = await _context.UserBlocks
			.Where(b => b.BlockerId == UserId || b.BlockedId == UserId)
			.Select(b => b.BlockerId == UserId ? b.BlockedId : b.BlockerId)
			.ToListAsync();

		var messages = await _context.Messages
			.Where(m => m.SenderId == UserId || m.ReceiverId == UserId)
			.Where(m => !m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted)
			.Where(m => !blockedIds.Contains(m.SenderId) && !blockedIds.Contains(m.ReceiverId) || m.SenderId == UserId || m.ReceiverId == UserId)
			.Where(m => !blockedIds.Contains(m.SenderId == UserId ? m.ReceiverId : m.SenderId))
			.Include(m => m.Sender)
			.Include(m => m.Receiver)
			.OrderByDescending(m => m.SentAt)
			.ToListAsync();

		var byOther = messages
			.GroupBy(m => m.SenderId == UserId ? m.ReceiverId : m.SenderId)
			.Select(g =>
			{
				var otherId = g.Key;
				var last = g.First();
				var other = last.SenderId == UserId ? last.Receiver : last.Sender;
				return new
				{
					otherUserId = otherId,
					otherUserName = (other.FirstName ?? "") + " " + (other.LastName ?? ""),
					otherUserEmail = other.Email,
					lastMessage = last.Content,
					lastMessageAt = last.SentAt,
					lastMessageFromMe = last.SenderId == UserId,
					unreadCount = g.Count(m => m.ReceiverId == UserId && m.ReadAt == null),
				};
			})
			.OrderByDescending(c => c.lastMessageAt)
			.ToList();

		var callerFaceBanned = await IsCallerFaceBannedInScopeAsync(cancellationToken);
		var superAdminIds = await MessengerModerationHelper.GetSuperAdminUserIdsAsync(
			_context,
			byOther.Select(c => c.otherUserId),
			cancellationToken);
		var filtered = byOther
			.Where(c => !MessengerModerationHelper.ShouldHidePeerConversation(
				callerFaceBanned,
				c.otherUserId,
				superAdminIds))
			.ToList();

		return Ok(filtered);
	}

	/// <summary>GET /api/messages/requests - Message requests from non-friends</summary>
	[HttpGet("requests")]
	public async Task<IActionResult> GetMessageRequests()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		// Get blocked user IDs (both directions)
		var blockedIds = await _context.UserBlocks
			.Where(b => b.BlockerId == UserId || b.BlockedId == UserId)
			.Select(b => b.BlockerId == UserId ? b.BlockedId : b.BlockerId)
			.ToListAsync();

		var requests = await _context.Messages
			.Where(m => m.ReceiverId == UserId && m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending)
			.Where(m => !blockedIds.Contains(m.SenderId))
			.Include(m => m.Sender)
			.GroupBy(m => m.SenderId)
			.Select(g => new
			{
				senderId = g.Key,
				sender = g.First().Sender,
				lastMessage = g.OrderByDescending(m => m.SentAt).First().Content,
				lastMessageAt = g.Max(m => m.SentAt),
				count = g.Count(),
			})
			.ToListAsync();

		var result = requests.Select(r => new
		{
			senderId = r.senderId,
			senderName = (r.sender.FirstName ?? "") + " " + (r.sender.LastName ?? ""),
			senderEmail = r.sender.Email,
			lastMessage = r.lastMessage,
			lastMessageAt = r.lastMessageAt,
			count = r.count,
		}).ToList();

		return Ok(result);
	}

	/// <summary>GET /api/messages/with/{otherUserId} - Chat history with a user</summary>
	[HttpGet("with/{otherUserId}")]
	public async Task<IActionResult> GetMessages(string otherUserId, [FromQuery] MessageHistoryQuery query, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var callerFaceBanned = await IsCallerFaceBannedInScopeAsync(cancellationToken);
		var superAdminIds = await MessengerModerationHelper.GetSuperAdminUserIdsAsync(_context, new[] { otherUserId }, cancellationToken);
		if (MessengerModerationHelper.ShouldHidePeerConversation(callerFaceBanned, otherUserId, superAdminIds))
			return StatusCode(StatusCodes.Status403Forbidden, new { code = "face_banned", error = "Account restricted in this community" });

		// Check if blocked
		var isBlocked = await _context.UserBlocks
			.AnyAsync(b =>
				(b.BlockerId == UserId && b.BlockedId == otherUserId) ||
				(b.BlockerId == otherUserId && b.BlockedId == UserId), cancellationToken);
		if (isBlocked)
			return Ok(Array.Empty<object>());

		var messages = await _context.Messages
			.Where(m =>
				((m.SenderId == UserId && m.ReceiverId == otherUserId) ||
				 (m.SenderId == otherUserId && m.ReceiverId == UserId)) &&
				(!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted))
			.Include(m => m.Sender).ThenInclude(u => u.UserRole)
			.OrderByDescending(m => m.SentAt)
			.Take(query.Limit)
			.ToListAsync(cancellationToken);

		messages.Reverse();

		return Ok(messages.Select(m => new
		{
			id = m.Id,
			senderId = m.SenderId,
			senderName = (m.Sender.FirstName ?? "") + " " + (m.Sender.LastName ?? ""),
			senderGlobalRole = m.Sender.UserRole?.Name,
			isPlatformAdministrator = OperatorModerationGuard.IsGlobalSuperAdminRole(m.Sender.UserRole?.Name),
			content = m.Content,
			sentAt = m.SentAt,
			readAt = m.ReadAt,
		}));
	}

	/// <summary>POST /api/messages/with/{otherUserId}/read - Mark messages as read</summary>
	[HttpPost("with/{otherUserId}/read")]
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
		return Ok(new { count = toUpdate.Count });
	}
}
