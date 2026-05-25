using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserFollowsController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<UserFollowsController> _logger;

	public UserFollowsController(
		ApplicationDbContext context,
		ILogger<UserFollowsController> logger)
	{
		_context = context;
		_logger = logger;
	}

	private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

	/// <summary>GET /api/userfollows/following - Users I follow</summary>
	[HttpGet("following")]
	public async Task<IActionResult> GetFollowing()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var following = await _context.UserFollows
			.Where(f => f.FollowerId == UserId)
			.Include(f => f.Followed)
			.OrderByDescending(f => f.CreatedAt)
			.Select(f => new
			{
				id = f.Id,
				userId = f.FollowedId,
				email = f.Followed.Email,
				name = (f.Followed.FirstName ?? "") + " " + (f.Followed.LastName ?? ""),
				createdAt = f.CreatedAt,
			})
			.ToListAsync();

		return Ok(following);
	}

	/// <summary>GET /api/userfollows/followers - Users who follow me</summary>
	[HttpGet("followers")]
	public async Task<IActionResult> GetFollowers()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var followers = await _context.UserFollows
			.Where(f => f.FollowedId == UserId)
			.Include(f => f.Follower)
			.OrderByDescending(f => f.CreatedAt)
			.Select(f => new
			{
				id = f.Id,
				userId = f.FollowerId,
				email = f.Follower.Email,
				name = (f.Follower.FirstName ?? "") + " " + (f.Follower.LastName ?? ""),
				createdAt = f.CreatedAt,
			})
			.ToListAsync();

		return Ok(followers);
	}

	/// <summary>GET /api/userfollows/status/{userId} - Check if I follow a user</summary>
	[HttpGet("status/{userId}")]
	public async Task<IActionResult> GetFollowStatus(string userId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var isFollowing = await _context.UserFollows
			.AnyAsync(f => f.FollowerId == UserId && f.FollowedId == userId);

		return Ok(new { isFollowing });
	}

	/// <summary>POST /api/userfollows - Follow a user</summary>
	[HttpPost]
	public async Task<IActionResult> FollowUser([FromBody] FollowUserDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		if (string.IsNullOrEmpty(dto?.FollowedId) || dto.FollowedId == UserId)
			return BadRequest(new { error = "Invalid user" });

		var exists = await _context.UserFollows
			.AnyAsync(f => f.FollowerId == UserId && f.FollowedId == dto.FollowedId);

		if (exists)
			return BadRequest(new { error = "Already following" });

		_context.UserFollows.Add(new UserFollow
		{
			FollowerId = UserId,
			FollowedId = dto.FollowedId,
		});
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {Follower} followed user {Followed}", UserId, dto.FollowedId);
		return Ok(new { success = true });
	}

	/// <summary>DELETE /api/userfollows/{userId} - Unfollow a user</summary>
	[HttpDelete("{userId}")]
	public async Task<IActionResult> UnfollowUser(string userId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var follow = await _context.UserFollows
			.FirstOrDefaultAsync(f => f.FollowerId == UserId && f.FollowedId == userId);

		if (follow == null)
			return NotFound(new { error = "Follow not found" });

		_context.UserFollows.Remove(follow);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {Follower} unfollowed user {Followed}", UserId, userId);
		return Ok(new { success = true });
	}
}
