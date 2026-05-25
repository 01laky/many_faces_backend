using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendRequestsController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IHubContext<MessengerHub> _hubContext;
	private readonly ILogger<FriendRequestsController> _logger;

	public FriendRequestsController(
		ApplicationDbContext context,
		IHubContext<MessengerHub> hubContext,
		ILogger<FriendRequestsController> logger)
	{
		_context = context;
		_hubContext = hubContext;
		_logger = logger;
	}

	private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

	/// <summary>GET /api/friendrequests - Pending requests received by current user</summary>
	[HttpGet]
	public async Task<IActionResult> GetPending()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		// Exclude blocked users
		var blockedIds = await _context.UserBlocks
			.Where(b => b.BlockerId == UserId || b.BlockedId == UserId)
			.Select(b => b.BlockerId == UserId ? b.BlockedId : b.BlockerId)
			.ToListAsync();

		var requests = await _context.FriendRequests
			.Where(r => r.ReceiverId == UserId && r.Status == FriendRequestStatus.Pending)
			.Where(r => !blockedIds.Contains(r.SenderId))
			.Include(r => r.Sender)
			.OrderByDescending(r => r.CreatedAt)
			.Select(r => new
			{
				id = r.Id,
				senderId = r.SenderId,
				senderEmail = r.Sender.Email,
				senderName = r.Sender.FirstName + " " + r.Sender.LastName,
				createdAt = r.CreatedAt,
			})
			.ToListAsync();

		return Ok(requests);
	}

	/// <summary>POST /api/friendrequests - Send friend request</summary>
	[HttpPost]
	public async Task<IActionResult> Send([FromBody] SendFriendRequestDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		if (string.IsNullOrEmpty(dto?.ReceiverId) || dto.ReceiverId == UserId)
			return BadRequest();

		// Check if either user has blocked the other
		var isBlocked = await _context.UserBlocks
			.AnyAsync(b =>
				(b.BlockerId == UserId && b.BlockedId == dto.ReceiverId) ||
				(b.BlockerId == dto.ReceiverId && b.BlockedId == UserId));
		if (isBlocked)
			return BadRequest(new { error = "Cannot send friend request to this user" });

		var exists = await _context.FriendRequests
			.AnyAsync(r =>
				(r.SenderId == UserId && r.ReceiverId == dto.ReceiverId) ||
				(r.SenderId == dto.ReceiverId && r.ReceiverId == UserId));

		if (exists)
			return BadRequest(new { error = "Friend request already exists" });

		var areFriends = await IsFriendWith(UserId, dto.ReceiverId);
		if (areFriends)
			return BadRequest(new { error = "Already friends" });

		var sender = await _context.Users.FindAsync(UserId);
		var senderName = sender != null ? (sender.FirstName ?? "") + " " + (sender.LastName ?? "") : "";

		_context.FriendRequests.Add(new FriendRequest
		{
			SenderId = UserId,
			ReceiverId = dto.ReceiverId,
			Status = FriendRequestStatus.Pending,
		});

		var notification = new Notification
		{
			UserId = dto.ReceiverId,
			Title = "Friend request",
			Message = $"{senderName.Trim()} wants to be your friend.",
			Type = "friend_request",
		};
		_context.Notifications.Add(notification);
		await _context.SaveChangesAsync();

		await _hubContext.Clients.User(dto.ReceiverId).SendAsync("ReceiveFriendRequest", UserId, senderName);
		await _hubContext.Clients.User(dto.ReceiverId).SendAsync("ReceiveNotification", notification.Id, notification.Title, notification.Message, notification.Type, notification.CreatedAt);
		_logger.LogInformation("User {Sender} sent friend request to {Receiver}", UserId, dto.ReceiverId);
		return Ok(new { success = true });
	}

	/// <summary>POST /api/friendrequests/{id}/accept</summary>
	[HttpPost("{id}/accept")]
	public async Task<IActionResult> Accept(int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var req = await _context.FriendRequests
			.Include(r => r.Sender)
			.Include(r => r.Receiver)
			.FirstOrDefaultAsync(r => r.Id == id && r.ReceiverId == UserId && r.Status == FriendRequestStatus.Pending);

		if (req == null)
			return NotFound();

		req.Status = FriendRequestStatus.Accepted;
		req.RespondedAt = DateTime.UtcNow;

		await AddFriendship(req.SenderId, req.ReceiverId);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {UserId} accepted friend request from {SenderId}", UserId, req.SenderId);
		return Ok(new { success = true });
	}

	/// <summary>POST /api/friendrequests/{id}/reject</summary>
	[HttpPost("{id}/reject")]
	public async Task<IActionResult> Reject(int id)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var req = await _context.FriendRequests
			.FirstOrDefaultAsync(r => r.Id == id && r.ReceiverId == UserId && r.Status == FriendRequestStatus.Pending);

		if (req == null)
			return NotFound();

		req.Status = FriendRequestStatus.Rejected;
		req.RespondedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		return Ok(new { success = true });
	}

	private async Task<bool> IsFriendWith(string a, string b)
	{
		return await _context.Friendships
			.AnyAsync(f => (f.UserId == a && f.FriendId == b) || (f.UserId == b && f.FriendId == a));
	}

	private async Task AddFriendship(string senderId, string receiverId)
	{
		if (await IsFriendWith(senderId, receiverId))
			return;
		_context.Friendships.Add(new Friendship { UserId = senderId, FriendId = receiverId });
	}
}
