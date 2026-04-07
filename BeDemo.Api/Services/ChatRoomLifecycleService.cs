using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

public sealed class ChatRoomLifecycleService : IChatRoomLifecycleService
{
    private readonly ApplicationDbContext _context;
    private readonly IRedisJobQueue _jobQueue;
    private readonly IHubContext<ChatRoomHub> _chatRoomHub;
    private readonly IHubContext<MessengerHub> _messengerHub;
    private readonly ILogger<ChatRoomLifecycleService> _logger;

    public ChatRoomLifecycleService(
        ApplicationDbContext context,
        IRedisJobQueue jobQueue,
        IHubContext<ChatRoomHub> chatRoomHub,
        IHubContext<MessengerHub> messengerHub,
        ILogger<ChatRoomLifecycleService> logger)
    {
        _context = context;
        _jobQueue = jobQueue;
        _chatRoomHub = chatRoomHub;
        _messengerHub = messengerHub;
        _logger = logger;
    }

    public Task ScheduleIdleCheckAsync(int faceChatRoomId, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { faceChatRoomId });
        return _jobQueue.ScheduleAsync("chatroom.idle-check", payload, DateTime.UtcNow.AddHours(1), cancellationToken);
    }

    public async Task ProcessIdleCheckAsync(int faceChatRoomId, CancellationToken cancellationToken = default)
    {
        var room = await _context.FaceChatRooms
            .FirstOrDefaultAsync(r => r.Id == faceChatRoomId, cancellationToken);
        if (room == null)
            return;

        var lastActivity = room.LastMessageAt ?? room.CreatedAt;
        if (DateTime.UtcNow - lastActivity < TimeSpan.FromHours(1))
        {
            await _jobQueue.ScheduleAsync(
                "chatroom.idle-check",
                JsonSerializer.Serialize(new { faceChatRoomId }),
                DateTime.UtcNow.AddHours(1),
                cancellationToken);
            _logger.LogDebug("Chat room {RoomId} still active, rescheduled idle check", faceChatRoomId);
            return;
        }

        await DeleteRoomCompletelyAsync(faceChatRoomId, "inactive", notifyCreatorIdleExpiry: true, cancellationToken);
    }

    public async Task DeleteRoomCompletelyAsync(
        int faceChatRoomId,
        string reason,
        bool notifyCreatorIdleExpiry,
        CancellationToken cancellationToken = default)
    {
        var room = await _context.FaceChatRooms.FirstOrDefaultAsync(r => r.Id == faceChatRoomId, cancellationToken);
        if (room == null)
            return;

        var creatorId = room.CreatorUserId;
        var title = room.Title;

        _context.FaceChatRoomJoinRequests.RemoveRange(
            await _context.FaceChatRoomJoinRequests.Where(x => x.FaceChatRoomId == faceChatRoomId).ToListAsync(cancellationToken));
        _context.FaceChatRoomMessages.RemoveRange(
            await _context.FaceChatRoomMessages.Where(x => x.FaceChatRoomId == faceChatRoomId).ToListAsync(cancellationToken));
        _context.FaceChatRoomMembers.RemoveRange(
            await _context.FaceChatRoomMembers.Where(x => x.FaceChatRoomId == faceChatRoomId).ToListAsync(cancellationToken));
        _context.FaceChatRooms.Remove(room);
        await _context.SaveChangesAsync(cancellationToken);

        await _chatRoomHub.Clients.Group(ChatRoomHub.RoomGroupName(faceChatRoomId))
            .SendAsync("ChatRoomClosed", faceChatRoomId, reason, cancellationToken: cancellationToken);

        if (notifyCreatorIdleExpiry && !string.IsNullOrEmpty(creatorId))
        {
            var notification = new Notification
            {
                UserId = creatorId,
                Title = "Chat room closed",
                Message = $"\"{title}\" was closed after 1 hour without messages.",
                Type = "chat_room_closed",
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);
            await _messengerHub.Clients.User(creatorId).SendAsync(
                "ReceiveNotification",
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.CreatedAt,
                cancellationToken);
        }

        _logger.LogInformation("Deleted chat room {RoomId} reason={Reason}", faceChatRoomId, reason);
    }
}
