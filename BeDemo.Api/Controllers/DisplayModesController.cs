using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DisplayModesController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<DisplayModesController> _logger;

	public DisplayModesController(
		ApplicationDbContext context,
		ILogger<DisplayModesController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/displaymodes - Get all display modes</summary>
	[HttpGet]
	public async Task<IActionResult> GetDisplayModes()
	{
		var displayModes = await _context.DisplayModes
			.OrderBy(dm => dm.Id)
			.Select(dm => new
			{
				id = dm.Id,
				index = dm.Index,
				name = dm.Name,
				createdAt = dm.CreatedAt,
			})
			.ToListAsync();

		_logger.LogInformation("Retrieved {Count} display modes", displayModes.Count);
		return Ok(displayModes);
	}

	/// <summary>GET /api/displaymodes/{id} - Get display mode by ID</summary>
	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetDisplayMode(int id)
	{
		var dm = await _context.DisplayModes.FindAsync(id);
		if (dm == null)
			return NotFound(new { error = "Display mode not found" });

		return Ok(new
		{
			id = dm.Id,
			index = dm.Index,
			name = dm.Name,
			createdAt = dm.CreatedAt,
		});
	}
}
