using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Hubs;

[Authorize]
public class ChatRoomHub : Hub
{
	private readonly ApplicationDbContext _context;
	private readonly IChatRoomLifecycleService _lifecycle;
	private readonly ILogger<ChatRoomHub> _logger;

	private readonly IFaceScopeContext _faceScope;

	public ChatRoomHub(
		ApplicationDbContext context,
		IChatRoomLifecycleService lifecycle,
		ILogger<ChatRoomHub> logger,
		IFaceScopeContext faceScope)
	{
		_context = context;
		_lifecycle = lifecycle;
		_logger = logger;
		_faceScope = faceScope;
	}

	private string? UserId =>
		Context.UserIdentifier ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

	public static string RoomGroupName(int roomId) => $"chatroom_{roomId}";

	public async Task JoinRoom(int faceChatRoomId)
	{
		if (string.IsNullOrEmpty(UserId))
			return;

		var room = await _context.FaceChatRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == faceChatRoomId);
		if (room == null)
			return;

		if (_faceScope.IsAvailable && !_faceScope.IsAdminFaceScope && room.FaceId != _faceScope.FaceId)
			return;

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, room.FaceId))
			return;

		var isMember = await _context.FaceChatRoomMembers
			.AnyAsync(m => m.FaceChatRoomId == faceChatRoomId && m.UserId == UserId);
		if (!isMember)
			return;

		await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupName(faceChatRoomId));
	}

	public async Task LeaveRoom(int faceChatRoomId)
	{
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupName(faceChatRoomId));
	}

	public async Task SendRoomMessage(int faceChatRoomId, string content)
	{
		if (string.IsNullOrEmpty(UserId) || string.IsNullOrWhiteSpace(content))
			return;

		var room = await _context.FaceChatRooms.FirstOrDefaultAsync(r => r.Id == faceChatRoomId);
		if (room == null)
			return;

		if (_faceScope.IsAvailable && !_faceScope.IsAdminFaceScope && room.FaceId != _faceScope.FaceId)
			return;

		if (await FaceChatRoomAuth.IsHostInFaceAsync(_context, UserId, room.FaceId))
			return;

		var isMember = await _context.FaceChatRoomMembers
			.AnyAsync(m => m.FaceChatRoomId == faceChatRoomId && m.UserId == UserId);
		if (!isMember)
			return;

		var trimmed = content.Trim();
		if (trimmed.Length > 8000)
			trimmed = trimmed[..8000];

		var msg = new FaceChatRoomMessage
		{
			FaceChatRoomId = faceChatRoomId,
			SenderUserId = UserId,
			Content = trimmed,
			SentAt = DateTime.UtcNow,
		};
		_context.FaceChatRoomMessages.Add(msg);
		room.LastMessageAt = msg.SentAt;
		room.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		await _lifecycle.ScheduleIdleCheckAsync(faceChatRoomId);

		var sender = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId);
		var senderName = sender != null ? $"{sender.FirstName} {sender.LastName}".Trim() : UserId;

		await Clients.Group(RoomGroupName(faceChatRoomId)).SendAsync(
			"ReceiveRoomMessage",
			faceChatRoomId,
			UserId,
			senderName,
			trimmed,
			msg.SentAt,
			msg.Id);

		_logger.LogDebug("Chat room {RoomId} message {MessageId}", faceChatRoomId, msg.Id);
	}
}
