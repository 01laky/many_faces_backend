using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendRequestsController : ApiControllerBase
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

	/// <summary>GET /api/friendrequests - Pending requests received by current user</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<FriendRequestItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetPending()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		// Exclude blocked users
		var blockedIds = await _context.UserBlocks
			.Where(b => b.BlockerId == UserId || b.BlockedId == UserId)
			.Select(b => b.BlockerId == UserId ? b.BlockedId : b.BlockerId)
			.ToListAsync();

		// Include removed: EF Core translates Sender nav-prop access inside Select without it.
		var requests = await _context.FriendRequests
			.AsNoTracking()
			.Where(r => r.ReceiverId == UserId && r.Status == FriendRequestStatus.Pending)
			.Where(r => !blockedIds.Contains(r.SenderId))
			.OrderByDescending(r => r.CreatedAt)
			.Take(200)
			.Select(r => new FriendRequestItemDto
			{
				Id = r.Id,
				SenderId = r.SenderId,
				SenderEmail = r.Sender.Email,
				SenderName = (r.Sender.FirstName ?? "") + " " + (r.Sender.LastName ?? ""),
				CreatedAt = r.CreatedAt,
			})
			.ToListAsync();

		return Ok(requests);
	}

	/// <summary>POST /api/friendrequests - Send friend request</summary>
	[HttpPost]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
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
			return BadRequest(new ErrorResponseDto { Error = "Cannot send friend request to this user" });

		var exists = await _context.FriendRequests
			.AnyAsync(r =>
				(r.SenderId == UserId && r.ReceiverId == dto.ReceiverId) ||
				(r.SenderId == dto.ReceiverId && r.ReceiverId == UserId));

		if (exists)
			return BadRequest(new ErrorResponseDto { Error = "Friend request already exists" });

		var areFriends = await IsFriendWith(UserId, dto.ReceiverId);
		if (areFriends)
			return BadRequest(new ErrorResponseDto { Error = "Already friends" });

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
		return Ok(SuccessResultDto.True);
	}

	/// <summary>POST /api/friendrequests/{id}/accept</summary>
	[HttpPost("{id}/accept")]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
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
		return Ok(SuccessResultDto.True);
	}

	/// <summary>POST /api/friendrequests/{id}/reject</summary>
	[HttpPost("{id}/reject")]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
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

		return Ok(SuccessResultDto.True);
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
