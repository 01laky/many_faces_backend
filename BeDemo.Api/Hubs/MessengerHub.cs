/*
 * MessengerHub.cs - SignalR Hub for friend requests and real-time messaging
 *
 * Endpoint (after RoutingMiddleware): /{face-kebab}/hubs/messenger?access_token=<JWT>
 *
 * Methods:
 * - SendChatMessage(receiverId, content) — peer / message-request flow
 * - SendPlatformDirectMessage(receiverId, content) — super-admin platform DMs only
 * - AcceptMessageRequest(senderId)
 * - RejectMessageRequest(senderId)
 *
 * Callbacks:
 * - ReceiveChatMessage(senderId, senderName, content, sentAt, messageId) — five arguments
 * - ReceiveMessageRequest(senderId, senderName, content, sentAt)
 * - ReceivePlatformChatError(code)
 * - ReceiveFriendRequest(senderId, senderName)
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Hubs;

[Authorize]
public class MessengerHub : Hub
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<MessengerHub> _logger;
	private readonly IFaceScopeContext _faceScope;
	private readonly IFaceModerationService _faceModeration;
	private readonly IPlatformDirectMessageService _platformDirectMessages;
	private readonly IPlatformChatRateLimiter _platformChatRateLimiter;
	private readonly IHubUserDisplayCache _hubUserDisplay;

	public MessengerHub(
		ApplicationDbContext context,
		ILogger<MessengerHub> logger,
		IFaceScopeContext faceScope,
		IFaceModerationService faceModeration,
		IPlatformDirectMessageService platformDirectMessages,
		IPlatformChatRateLimiter platformChatRateLimiter,
		IHubUserDisplayCache hubUserDisplay)
	{
		_context = context;
		_logger = logger;
		_faceScope = faceScope;
		_faceModeration = faceModeration;
		_platformDirectMessages = platformDirectMessages;
		_platformChatRateLimiter = platformChatRateLimiter;
		_hubUserDisplay = hubUserDisplay;
	}

	private string? UserId => Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

	private bool CanManageAllFaces() =>
		Context.User != null && PlatformAccessRules.CanManageAllFaces(_faceScope, Context.User);

	private async Task<bool> EnforceTenantSocialPairAsync(string otherUserId)
	{
		if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(otherUserId))
			return false;
		if (!_faceScope.IsAvailable)
			return false;
		if (CanManageAllFaces())
			return true;
		return await TenantSocialScopeRules.BothUsersParticipateInFaceAsync(_context, _faceScope.FaceId, UserId, otherUserId);
	}

	/// <summary>Super-admin platform DM — bypasses friendship / message-request (admin face only).</summary>
	public async Task SendPlatformDirectMessage(string receiverId, string content)
	{
		if (string.IsNullOrEmpty(UserId))
			return;

		if (!_platformChatRateLimiter.TryAllow(UserId))
		{
			await Clients.Caller.SendAsync("ReceivePlatformChatError", OperatorUserChatHubErrorCodes.RateLimited);
			return;
		}

		var sender = await _context.Users.Include(u => u.UserRole).FirstOrDefaultAsync(u => u.Id == UserId);
		if (sender == null || !OperatorModerationGuard.IsGlobalSuperAdminRole(sender.UserRole?.Name))
		{
			await Clients.Caller.SendAsync("ReceivePlatformChatError", OperatorUserChatHubErrorCodes.NotSuperAdmin);
			return;
		}

		var (errorCode, _) = await _platformDirectMessages.SendAsync(UserId, receiverId, content, Context.ConnectionAborted);
		if (errorCode != null)
			await Clients.Caller.SendAsync("ReceivePlatformChatError", errorCode);
	}

	public async Task SendChatMessage(string receiverId, string content)
	{
		if (string.IsNullOrEmpty(UserId) || string.IsNullOrWhiteSpace(content) || string.IsNullOrEmpty(receiverId))
			return;

		var sender = await _context.Users.Include(u => u.UserRole).FirstOrDefaultAsync(u => u.Id == UserId);
		var receiver = await _context.Users.Include(u => u.UserRole).FirstOrDefaultAsync(u => u.Id == receiverId);
		if (sender == null || receiver == null)
			return;

		var senderIsSuper = OperatorModerationGuard.IsGlobalSuperAdminRole(sender.UserRole?.Name);
		var receiverIsSuper = OperatorModerationGuard.IsGlobalSuperAdminRole(receiver.UserRole?.Name);
		var isPlatformPair = (senderIsSuper && !receiverIsSuper) || (receiverIsSuper && !senderIsSuper);

		// Platform DMs bypass tenant face membership — super-admins are not on every tenant face.
		if (!isPlatformPair && !await EnforceTenantSocialPairAsync(receiverId))
			return;

		if (!isPlatformPair && _faceScope.IsAvailable && await _faceModeration.ShouldBlockPeerActivityInFaceAsync(UserId, _faceScope.FaceId))
			return;

		// Super-admin → end user: platform DM channel (also used from admin User chat UI).
		if (senderIsSuper && !receiverIsSuper)
		{
			if (!_platformChatRateLimiter.TryAllow(UserId))
			{
				await Clients.Caller.SendAsync("ReceivePlatformChatError", OperatorUserChatHubErrorCodes.RateLimited);
				return;
			}

			var (code, _) = await _platformDirectMessages.SendAsync(UserId, receiverId, content, Context.ConnectionAborted);
			if (code != null)
				await Clients.Caller.SendAsync("ReceivePlatformChatError", code);
			return;
		}

		// End user → super-admin: allowed only when a platform thread already exists.
		if (receiverIsSuper && !senderIsSuper)
		{
			var (code, _) = await _platformDirectMessages.SendAsync(UserId, receiverId, content, Context.ConnectionAborted);
			if (code != null)
				await Clients.Caller.SendAsync("ReceivePlatformChatError", code);
			return;
		}

		if (senderIsSuper && receiverIsSuper)
			return;

		var areFriends = await _context.Friendships
			.AnyAsync(f => (f.UserId == UserId && f.FriendId == receiverId) || (f.UserId == receiverId && f.FriendId == UserId));

		var isMessageRequest = !areFriends;

		var senderName = await ResolveDisplayNameAsync(UserId, sender);

		var message = new Message
		{
			SenderId = UserId,
			ReceiverId = receiverId,
			Content = content.Trim(),
			IsMessageRequest = isMessageRequest,
			MessageRequestStatus = isMessageRequest ? MessageRequestStatus.Pending : null,
		};
		_context.Messages.Add(message);

		Notification? notification = null;
		if (isMessageRequest)
		{
			notification = new Notification
			{
				UserId = receiverId,
				Title = "Message request",
				Message = $"{senderName.Trim()}: {content.Trim()}",
				Type = "message_request",
			};
			_context.Notifications.Add(notification);
		}

		await _context.SaveChangesAsync();

		if (isMessageRequest && notification != null)
		{
			await Clients.User(receiverId).SendAsync("ReceiveMessageRequest", UserId, senderName, content.Trim(), message.SentAt);
			await Clients.User(receiverId).SendAsync("ReceiveNotification", notification.Id, notification.Title, notification.Message, notification.Type, notification.CreatedAt);
		}
		else
		{
			await Clients.User(receiverId).SendAsync("ReceiveChatMessage", UserId, senderName, content.Trim(), message.SentAt, message.Id);
		}
	}

	public async Task AcceptMessageRequest(string senderId)
	{
		if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(senderId))
			return;

		if (!await EnforceTenantSocialPairAsync(senderId))
			return;

		var requests = await _context.Messages
			.Where(m => m.SenderId == senderId && m.ReceiverId == UserId && m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending)
			.ToListAsync();

		if (requests.Count == 0)
			return;

		foreach (var m in requests)
			m.MessageRequestStatus = MessageRequestStatus.Accepted;

		var exists = await _context.Friendships
			.AnyAsync(f => (f.UserId == UserId && f.FriendId == senderId) || (f.UserId == senderId && f.FriendId == UserId));
		if (!exists)
			_context.Friendships.Add(new Friendship { UserId = UserId, FriendId = senderId });

		await _context.SaveChangesAsync();

		var sender = await _context.Users.FindAsync(senderId);
		var senderName = await ResolveDisplayNameAsync(senderId, sender);

		await Clients.User(senderId).SendAsync("MessageRequestAccepted", UserId, senderName);
	}

	public async Task RejectMessageRequest(string senderId)
	{
		if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(senderId))
			return;

		if (!await EnforceTenantSocialPairAsync(senderId))
			return;

		var requests = await _context.Messages
			.Where(m => m.SenderId == senderId && m.ReceiverId == UserId && m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending)
			.ToListAsync();

		foreach (var m in requests)
			m.MessageRequestStatus = MessageRequestStatus.Rejected;

		await _context.SaveChangesAsync();
		await Clients.User(senderId).SendAsync("MessageRequestRejected", UserId);
	}

	private async Task<string> ResolveDisplayNameAsync(string userId, ApplicationUser? loaded = null)
	{
		var cached = await _hubUserDisplay.GetAsync(userId, Context.ConnectionAborted);
		if (cached.HasValue)
			return cached.Value.DisplayName;

		var user = loaded ?? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
		return user == null ? string.Empty : $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim();
	}
}
