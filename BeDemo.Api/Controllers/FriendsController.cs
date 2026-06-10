using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;

	public FriendsController(ApplicationDbContext context)
	{
		_context = context;
	}

	/// <summary>GET /api/friends - List current user's friends</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IEnumerable<FriendSummaryDto>), StatusCodes.Status200OK)]
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
			.Select(u => new FriendSummaryDto { Id = u.Id, Email = u.Email, FirstName = u.FirstName, LastName = u.LastName })
			.ToListAsync();

		return Ok(users);
	}
}
