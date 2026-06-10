using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Social;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;

	public NotificationsController(ApplicationDbContext context)
	{
		_context = context;
	}

	/// <summary>GET /api/notifications - User's notification history (newest first)</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<NotificationHistoryItemDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetHistory([FromQuery] NotificationsListQuery query)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var items = await _context.Notifications
			.Where(n => n.UserId == UserId)
			.OrderByDescending(n => n.CreatedAt)
			.Take(query.Limit)
			.Select(n => new NotificationHistoryItemDto
			{
				Id = n.Id,
				Title = n.Title,
				Message = n.Message,
				Type = n.Type,
				CreatedAt = n.CreatedAt,
			})
			.ToListAsync();

		return Ok(items);
	}
}
