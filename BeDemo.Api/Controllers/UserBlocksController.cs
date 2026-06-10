using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserBlocksController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<UserBlocksController> _logger;

	public UserBlocksController(
		ApplicationDbContext context,
		ILogger<UserBlocksController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/userblocks - List users blocked by current user</summary>
	[HttpGet]
	public async Task<IActionResult> GetBlockedUsers()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var blocked = await _context.UserBlocks
			.Where(b => b.BlockerId == UserId)
			.Include(b => b.Blocked)
			.OrderByDescending(b => b.CreatedAt)
			.Select(b => new
			{
				id = b.Id,
				blockedId = b.BlockedId,
				blockedEmail = b.Blocked.Email,
				blockedName = (b.Blocked.FirstName ?? "") + " " + (b.Blocked.LastName ?? ""),
				createdAt = b.CreatedAt,
			})
			.ToListAsync();

		return Ok(blocked);
	}

	/// <summary>GET /api/userblocks/status/{userId} - Check if a user is blocked</summary>
	[HttpGet("status/{userId}")]
	public async Task<IActionResult> GetBlockStatus(string userId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var isBlocked = await _context.UserBlocks
			.AnyAsync(b => b.BlockerId == UserId && b.BlockedId == userId);

		return Ok(new { isBlocked });
	}

	/// <summary>POST /api/userblocks - Block a user</summary>
	[HttpPost]
	public async Task<IActionResult> BlockUser([FromBody] BlockUserDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		if (string.IsNullOrEmpty(dto?.BlockedId) || dto.BlockedId == UserId)
			return BadRequest(new { error = "Invalid user" });

		var exists = await _context.UserBlocks
			.AnyAsync(b => b.BlockerId == UserId && b.BlockedId == dto.BlockedId);

		if (exists)
			return BadRequest(new { error = "User already blocked" });

		_context.UserBlocks.Add(new UserBlock
		{
			BlockerId = UserId,
			BlockedId = dto.BlockedId,
		});
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {Blocker} blocked user {Blocked}", UserId, dto.BlockedId);
		return Ok(new { success = true });
	}

	/// <summary>DELETE /api/userblocks/{userId} - Unblock a user</summary>
	[HttpDelete("{userId}")]
	public async Task<IActionResult> UnblockUser(string userId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var block = await _context.UserBlocks
			.FirstOrDefaultAsync(b => b.BlockerId == UserId && b.BlockedId == userId);

		if (block == null)
			return NotFound(new { error = "Block not found" });

		_context.UserBlocks.Remove(block);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {Blocker} unblocked user {Blocked}", UserId, userId);
		return Ok(new { success = true });
	}
}
