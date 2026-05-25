using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Central persistence and SignalR fan-out for super-admin platform DMs.
/// Enforces role rules, thread existence for user→super-admin replies, and notification titles.
/// </summary>
public sealed class PlatformDirectMessageService : IPlatformDirectMessageService
{
	private readonly ApplicationDbContext _context;
	private readonly IHubContext<MessengerHub> _messengerHub;
	private readonly ILogger<PlatformDirectMessageService> _logger;

	public PlatformDirectMessageService(
		ApplicationDbContext context,
		IHubContext<MessengerHub> messengerHub,
		ILogger<PlatformDirectMessageService> logger)
	{
		_context = context;
		_messengerHub = messengerHub;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<(string? HubErrorCode, int? MessageId)> SendAsync(
		string senderId,
		string receiverId,
		string content,
		CancellationToken cancellationToken = default)
	{
		// Validate payload before any DB work so hub/REST return stable error codes.
		if (string.IsNullOrWhiteSpace(content))
			return (OperatorUserChatHubErrorCodes.EmptyContent, null);

		var trimmed = content.Trim();
		if (trimmed.Length > PlatformDirectMessageRules.MaxContentLength)
			return (OperatorUserChatHubErrorCodes.MessageTooLong, null);

		if (string.Equals(senderId, receiverId, StringComparison.Ordinal))
			return (OperatorUserChatHubErrorCodes.CannotMessageSelf, null);

		var sender = await _context.Users.Include(u => u.UserRole).FirstOrDefaultAsync(u => u.Id == senderId, cancellationToken);
		var receiver = await _context.Users.Include(u => u.UserRole).FirstOrDefaultAsync(u => u.Id == receiverId, cancellationToken);
		if (sender == null || receiver == null)
			return (OperatorUserChatHubErrorCodes.TargetNotFound, null);

		var senderIsSuper = OperatorModerationGuard.IsGlobalSuperAdminRole(sender.UserRole?.Name);
		var receiverIsSuper = OperatorModerationGuard.IsGlobalSuperAdminRole(receiver.UserRole?.Name);

		// Two super-admins cannot open a platform dyad.
		if (senderIsSuper && receiverIsSuper)
			return (OperatorUserChatHubErrorCodes.CannotMessageSuperAdmin, null);

		// End user may reply only after a super-admin started the platform thread.
		if (receiverIsSuper && !senderIsSuper)
		{
			if (!await HasPlatformThreadAsync(senderId, receiverId, cancellationToken))
				return (OperatorUserChatHubErrorCodes.NoPlatformThread, null);
		}
		// Only super-admins may initiate platform DMs to regular users.
		else if (!senderIsSuper)
		{
			return (OperatorUserChatHubErrorCodes.NotSuperAdmin, null);
		}

		var message = new Message
		{
			SenderId = senderId,
			ReceiverId = receiverId,
			Content = trimmed,
			IsMessageRequest = false,
			MessageRequestStatus = null,
			IsPlatformDirectMessage = true,
		};
		_context.Messages.Add(message);

		var senderName = FormatDisplayName(sender);
		var notificationTitle = senderIsSuper
			? PlatformNotificationTitles.SuperAdminMessage(null)
			: senderName;
		var notification = new Notification
		{
			UserId = receiverId,
			Title = notificationTitle,
			Message = trimmed.Length > 120 ? trimmed[..120] + "…" : trimmed,
			Type = senderIsSuper ? "super_admin_message" : "message",
		};
		_context.Notifications.Add(notification);

		await _context.SaveChangesAsync(cancellationToken);

		// Five-argument ReceiveChatMessage contract (portal + admin); echo to sender for optimistic UI sync.
		await _messengerHub.Clients.User(receiverId).SendAsync(
			"ReceiveChatMessage",
			senderId,
			senderName,
			trimmed,
			message.SentAt,
			message.Id,
			cancellationToken);

		await _messengerHub.Clients.User(senderId).SendAsync(
			"ReceiveChatMessage",
			senderId,
			senderName,
			trimmed,
			message.SentAt,
			message.Id,
			cancellationToken);

		var correlationId = Guid.NewGuid().ToString("N");
		_logger.LogInformation(
			"OperatorUserChatSent operatorUserId={OperatorUserId} targetUserId={TargetUserId} messageId={MessageId} correlationId={CorrelationId}",
			senderId,
			receiverId,
			message.Id,
			correlationId);

		return (null, message.Id);
	}

	/// <inheritdoc />
	public Task<bool> ThreadExistsAsync(string operatorUserId, string targetUserId, CancellationToken cancellationToken = default) =>
		HasPlatformThreadAsync(operatorUserId, targetUserId, cancellationToken);

	/// <summary>
	/// True when the dyad already has a platform message, or legacy super-admin traffic before the flag column.
	/// </summary>
	private async Task<bool> HasPlatformThreadAsync(string userA, string userB, CancellationToken cancellationToken)
	{
		var superAdminRole = UserRole.GlobalRoleNames.SuperAdmin;
		return await (
			from m in _context.Messages.AsNoTracking()
			join s in _context.Users.AsNoTracking() on m.SenderId equals s.Id
			join role in _context.UserRoles.AsNoTracking() on s.UserRoleId equals role.Id
			where (m.SenderId == userA && m.ReceiverId == userB) || (m.SenderId == userB && m.ReceiverId == userA)
			where m.IsPlatformDirectMessage
				  || (!m.IsMessageRequest && role.Name == superAdminRole)
			select m.Id).AnyAsync(cancellationToken);
	}

	private static string FormatDisplayName(ApplicationUser user)
	{
		var name = ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim();
		return string.IsNullOrEmpty(name) ? user.Email ?? "User" : name;
	}
}
