using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserFollowsController : ApiControllerBase
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

	/// <summary>GET /api/userfollows/following - Users I follow</summary>
	[HttpGet("following")]
	[ProducesResponseType(typeof(IReadOnlyList<UserFollowItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetFollowing()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var following = await _context.UserFollows
			.Where(f => f.FollowerId == UserId)
			.Include(f => f.Followed)
			.OrderByDescending(f => f.CreatedAt)
			.Select(f => new UserFollowItemDto
			{
				Id = f.Id,
				UserId = f.FollowedId,
				Email = f.Followed.Email,
				Name = (f.Followed.FirstName ?? "") + " " + (f.Followed.LastName ?? ""),
				CreatedAt = f.CreatedAt,
			})
			.ToListAsync();

		return Ok(following);
	}

	/// <summary>GET /api/userfollows/followers - Users who follow me</summary>
	[HttpGet("followers")]
	[ProducesResponseType(typeof(IReadOnlyList<UserFollowItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetFollowers()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var followers = await _context.UserFollows
			.Where(f => f.FollowedId == UserId)
			.Include(f => f.Follower)
			.OrderByDescending(f => f.CreatedAt)
			.Select(f => new UserFollowItemDto
			{
				Id = f.Id,
				UserId = f.FollowerId,
				Email = f.Follower.Email,
				Name = (f.Follower.FirstName ?? "") + " " + (f.Follower.LastName ?? ""),
				CreatedAt = f.CreatedAt,
			})
			.ToListAsync();

		return Ok(followers);
	}

	/// <summary>GET /api/userfollows/status/{userId} - Check if I follow a user</summary>
	[HttpGet("status/{userId}")]
	[ProducesResponseType(typeof(IsFollowingDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetFollowStatus(string userId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var isFollowing = await _context.UserFollows
			.AnyAsync(f => f.FollowerId == UserId && f.FollowedId == userId);

		return Ok(new IsFollowingDto { IsFollowing = isFollowing });
	}

	/// <summary>POST /api/userfollows - Follow a user</summary>
	[HttpPost]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> FollowUser([FromBody] FollowUserDto dto)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();
		if (string.IsNullOrEmpty(dto?.FollowedId) || dto.FollowedId == UserId)
			return BadRequest(new ErrorResponseDto { Error = "Invalid user" });

		var exists = await _context.UserFollows
			.AnyAsync(f => f.FollowerId == UserId && f.FollowedId == dto.FollowedId);

		if (exists)
			return BadRequest(new ErrorResponseDto { Error = "Already following" });

		_context.UserFollows.Add(new UserFollow
		{
			FollowerId = UserId,
			FollowedId = dto.FollowedId,
		});
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {Follower} followed user {Followed}", UserId, dto.FollowedId);
		return Ok(SuccessResultDto.True);
	}

	/// <summary>DELETE /api/userfollows/{userId} - Unfollow a user</summary>
	[HttpDelete("{userId}")]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UnfollowUser(string userId)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var follow = await _context.UserFollows
			.FirstOrDefaultAsync(f => f.FollowerId == UserId && f.FollowedId == userId);

		if (follow == null)
			return NotFound(new ErrorResponseDto { Error = "Follow not found" });

		_context.UserFollows.Remove(follow);
		await _context.SaveChangesAsync();

		_logger.LogInformation("User {Follower} unfollowed user {Followed}", UserId, userId);
		return Ok(SuccessResultDto.True);
	}
}
