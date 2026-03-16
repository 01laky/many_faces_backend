using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

/// <summary>
/// GET /api/stats - Dashboard statistics for admin (users, friend requests, messages count).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StatsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        var usersCount = await _context.Users.CountAsync();
        var friendRequestsCount = await _context.FriendRequests
            .CountAsync(r => r.Status == FriendRequestStatus.Pending);
        var messagesCount = await _context.Messages.CountAsync();

        return Ok(new
        {
            usersCount,
            friendRequestsCount,
            messagesCount,
        });
    }
}
