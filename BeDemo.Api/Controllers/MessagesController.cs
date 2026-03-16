using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MessagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    /// <summary>GET /api/messages/conversations - List conversations with last message</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var messages = await _context.Messages
            .Where(m => m.SenderId == UserId || m.ReceiverId == UserId)
            .Where(m => !m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted)
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

        return Ok(byOther);
    }

    /// <summary>GET /api/messages/requests - Message requests from non-friends</summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetMessageRequests()
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var requests = await _context.Messages
            .Where(m => m.ReceiverId == UserId && m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending)
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
    public async Task<IActionResult> GetMessages(string otherUserId, [FromQuery] int limit = 50)
    {
        if (string.IsNullOrEmpty(UserId))
            return Unauthorized();

        var messages = await _context.Messages
            .Where(m =>
                ((m.SenderId == UserId && m.ReceiverId == otherUserId) ||
                 (m.SenderId == otherUserId && m.ReceiverId == UserId)) &&
                (!m.IsMessageRequest || m.MessageRequestStatus == MessageRequestStatus.Accepted))
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .ToListAsync();

        messages.Reverse();

        return Ok(messages.Select(m => new
        {
            id = m.Id,
            senderId = m.SenderId,
            senderName = (m.Sender.FirstName ?? "") + " " + (m.Sender.LastName ?? ""),
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
