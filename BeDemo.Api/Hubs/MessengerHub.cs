/*
 * MessengerHub.cs - SignalR Hub for friend requests and real-time messaging
 *
 * Endpoint: /hubs/messenger?access_token=<JWT_TOKEN>
 *
 * Methods:
 * - SendChatMessage(receiverId, content)
 * - AcceptMessageRequest(senderId)
 * - RejectMessageRequest(senderId)
 *
 * Callbacks:
 * - ReceiveChatMessage(senderId, content, sentAt, messageId)
 * - ReceiveMessageRequest(senderId, content, sentAt)
 * - ReceiveFriendRequest(senderId, senderName)
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Hubs;

[Authorize]
public class MessengerHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MessengerHub> _logger;

    public MessengerHub(ApplicationDbContext context, ILogger<MessengerHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string? UserId => Context.UserIdentifier ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    public override async Task OnConnectedAsync()
    {
        var userId = UserId;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = UserId;
        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendChatMessage(string receiverId, string content)
    {
        if (string.IsNullOrEmpty(UserId) || string.IsNullOrWhiteSpace(content) || string.IsNullOrEmpty(receiverId))
            return;

        var sender = await _context.Users.FindAsync(UserId);
        var receiver = await _context.Users.FindAsync(receiverId);
        if (sender == null || receiver == null)
            return;

        var areFriends = await _context.Friendships
            .AnyAsync(f => (f.UserId == UserId && f.FriendId == receiverId) || (f.UserId == receiverId && f.FriendId == UserId));

        var isMessageRequest = !areFriends;

        var senderName = (sender.FirstName ?? "") + " " + (sender.LastName ?? "");

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
        var senderName = sender != null ? (sender.FirstName ?? "") + " " + (sender.LastName ?? "") : "";

        await Clients.User(senderId).SendAsync("MessageRequestAccepted", UserId, senderName);
    }

    public async Task RejectMessageRequest(string senderId)
    {
        if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(senderId))
            return;

        var requests = await _context.Messages
            .Where(m => m.SenderId == senderId && m.ReceiverId == UserId && m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending)
            .ToListAsync();

        foreach (var m in requests)
            m.MessageRequestStatus = MessageRequestStatus.Rejected;

        await _context.SaveChangesAsync();
        await Clients.User(senderId).SendAsync("MessageRequestRejected", UserId);
    }
}
