using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComponentTypesController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<ComponentTypesController> _logger;

	public ComponentTypesController(
		ApplicationDbContext context,
		ILogger<ComponentTypesController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/componenttypes - Get all component types</summary>
	[HttpGet]
	[ProducesResponseType(typeof(IEnumerable<ComponentTypeDetailDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetComponentTypes()
	{
		var componentTypes = await _context.ComponentTypes
			.OrderBy(ct => ct.Id)
			.Select(ct => new ComponentTypeDetailDto
			{
				Id = ct.Id,
				Index = ct.Index,
				Name = ct.Name,
				CreatedAt = ct.CreatedAt,
			})
			.ToListAsync();

		_logger.LogInformation("Retrieved {Count} component types", componentTypes.Count);
		return Ok(componentTypes);
	}

	/// <summary>GET /api/componenttypes/{id} - Get component type by ID</summary>
	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(ComponentTypeDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetComponentType(int id)
	{
		var ct = await _context.ComponentTypes.FindAsync(id);
		if (ct == null)
			return NotFound(new ErrorResponseDto { Error = "Component type not found" });

		return Ok(new ComponentTypeDetailDto
		{
			Id = ct.Id,
			Index = ct.Index,
			Name = ct.Name,
			CreatedAt = ct.CreatedAt,
		});
	}
}
