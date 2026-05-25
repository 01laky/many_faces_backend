using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ControllerBase
{
	private readonly ApplicationDbContext _context;

	public FriendsController(ApplicationDbContext context)
	{
		_context = context;
	}

	private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

	/// <summary>GET /api/friends - List current user's friends</summary>
	[HttpGet]
	public async Task<IActionResult> GetFriends()
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var friends = await _context.Friendships
			.Where(f => f.UserId == UserId || f.FriendId == UserId)
			.Select(f => f.UserId == UserId ? f.FriendId : f.UserId)
			.ToListAsync();

		var users = await _context.Users
			.Where(u => friends.Contains(u.Id))
			.Select(u => new { id = u.Id, email = u.Email, firstName = u.FirstName, lastName = u.LastName })
			.ToListAsync();

		return Ok(users);
	}
}
