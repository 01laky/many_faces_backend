using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;

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
	[ProducesResponseType(typeof(IEnumerable<DisplayModeDetailDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetDisplayModes()
	{
		var displayModes = await _context.DisplayModes
			.OrderBy(dm => dm.Id)
			.Select(dm => new DisplayModeDetailDto
			{
				Id = dm.Id,
				Index = dm.Index,
				Name = dm.Name,
				CreatedAt = dm.CreatedAt,
			})
			.ToListAsync();

		_logger.LogInformation("Retrieved {Count} display modes", displayModes.Count);
		return Ok(displayModes);
	}

	/// <summary>GET /api/displaymodes/{id} - Get display mode by ID</summary>
	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(DisplayModeDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetDisplayMode(int id)
	{
		var dm = await _context.DisplayModes.FindAsync(id);
		if (dm == null)
			return NotFound(new ErrorResponseDto { Error = "Display mode not found" });

		return Ok(new DisplayModeDetailDto
		{
			Id = dm.Id,
			Index = dm.Index,
			Name = dm.Name,
			CreatedAt = dm.CreatedAt,
		});
	}
}
