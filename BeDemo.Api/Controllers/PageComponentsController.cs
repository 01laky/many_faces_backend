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
public class PageComponentsController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<PageComponentsController> _logger;

	public PageComponentsController(
		ApplicationDbContext context,
		ILogger<PageComponentsController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>GET /api/pagecomponents/page/{pageId} - Get all components for a page</summary>
	[HttpGet("page/{pageId:int}")]
	[ProducesResponseType(typeof(IEnumerable<PageComponentDetailDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetByPage(int pageId)
	{
		var raw = await _context.PageComponents
			.AsNoTracking()
			.Where(pc => pc.PageId == pageId)
			.Include(pc => pc.ComponentType)
			.Include(pc => pc.DisplayMode)
			.OrderBy(pc => pc.Y).ThenBy(pc => pc.X)
			.ToListAsync();

		var components = raw.Select(pc => new PageComponentDetailDto
		{
			Id = pc.Id,
			PageId = pc.PageId,
			GridKey = pc.GridKey,
			ComponentType = new PageComponentTypeRefDto
			{
				Id = pc.ComponentType.Id,
				Index = pc.ComponentType.Index,
				Name = pc.ComponentType.Name,
			},
			DisplayMode = new PageComponentDisplayModeRefDto
			{
				Id = pc.DisplayMode.Id,
				Index = pc.DisplayMode.Index,
				Name = pc.DisplayMode.Name,
			},
			X = pc.X,
			Y = pc.Y,
			W = pc.W,
			H = pc.H,
			MinW = pc.MinW,
			MinH = pc.MinH,
			Label = pc.Label,
			Title = pc.Title,
			Icon = pc.Icon,
			CreatedAt = pc.CreatedAt,
		});

		return Ok(components);
	}

	/// <summary>GET /api/pagecomponents/{id} - Get a single component</summary>
	[HttpGet("{id:int}")]
	[ProducesResponseType(typeof(PageComponentDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetById(int id)
	{
		var pc = await _context.PageComponents
			.Include(p => p.ComponentType)
			.Include(p => p.DisplayMode)
			.FirstOrDefaultAsync(p => p.Id == id);

		if (pc == null)
			return NotFound(new ErrorResponseDto { Error = "Page component not found" });

		return Ok(new PageComponentDetailDto
		{
			Id = pc.Id,
			PageId = pc.PageId,
			GridKey = pc.GridKey,
			ComponentType = new PageComponentTypeRefDto
			{
				Id = pc.ComponentType.Id,
				Index = pc.ComponentType.Index,
				Name = pc.ComponentType.Name,
			},
			DisplayMode = new PageComponentDisplayModeRefDto
			{
				Id = pc.DisplayMode.Id,
				Index = pc.DisplayMode.Index,
				Name = pc.DisplayMode.Name,
			},
			X = pc.X,
			Y = pc.Y,
			W = pc.W,
			H = pc.H,
			MinW = pc.MinW,
			MinH = pc.MinH,
			Label = pc.Label,
			Title = pc.Title,
			Icon = pc.Icon,
			CreatedAt = pc.CreatedAt,
		});
	}

	/// <summary>POST /api/pagecomponents - Create a new component on a page</summary>
	[HttpPost]
	[ProducesResponseType(typeof(PageComponentCreatedDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Create([FromBody] CreatePageComponentDto dto)
	{
		if (dto.PageId <= 0 || dto.ComponentTypeId <= 0 || dto.DisplayModeId <= 0)
			return BadRequest(new ErrorResponseDto { Error = "Invalid input" });

		var page = await _context.Pages.FindAsync(dto.PageId);
		if (page == null)
			return BadRequest(new ErrorResponseDto { Error = "Page not found" });

		var componentType = await _context.ComponentTypes.FindAsync(dto.ComponentTypeId);
		if (componentType == null)
			return BadRequest(new ErrorResponseDto { Error = "Component type not found" });

		var displayMode = await _context.DisplayModes.FindAsync(dto.DisplayModeId);
		if (displayMode == null)
			return BadRequest(new ErrorResponseDto { Error = "Display mode not found" });

		var gridKey = string.IsNullOrWhiteSpace(dto.GridKey)
			? $"item-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Random.Shared.Next(1000)}"
			: dto.GridKey;

		var entity = new PageComponent
		{
			PageId = dto.PageId,
			ComponentTypeId = dto.ComponentTypeId,
			DisplayModeId = dto.DisplayModeId,
			GridKey = gridKey,
			X = dto.X,
			Y = dto.Y,
			W = dto.W > 0 ? dto.W : 3,
			H = dto.H > 0 ? dto.H : 2,
			MinW = dto.MinW > 0 ? dto.MinW : 1,
			MinH = dto.MinH > 0 ? dto.MinH : 1,
			Label = dto.Label,
			Title = dto.Title,
			Icon = dto.Icon,
		};

		_context.PageComponents.Add(entity);
		await _context.SaveChangesAsync();

		_logger.LogInformation(
			"Created PageComponent {Id} on Page {PageId}: {ComponentType}/{DisplayMode}",
			entity.Id, dto.PageId, componentType.Index, displayMode.Index);

		return Ok(new PageComponentCreatedDto { Id = entity.Id, GridKey = gridKey });
	}

	/// <summary>PUT /api/pagecomponents/{id} - Update a component</summary>
	[HttpPut("{id:int}")]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Update(int id, [FromBody] UpdatePageComponentDto dto)
	{
		var entity = await _context.PageComponents.FindAsync(id);
		if (entity == null)
			return NotFound(new ErrorResponseDto { Error = "Page component not found" });

		if (dto.ComponentTypeId.HasValue)
		{
			var ct = await _context.ComponentTypes.FindAsync(dto.ComponentTypeId.Value);
			if (ct == null)
				return BadRequest(new ErrorResponseDto { Error = "Component type not found" });
			entity.ComponentTypeId = dto.ComponentTypeId.Value;
		}

		if (dto.DisplayModeId.HasValue)
		{
			var dm = await _context.DisplayModes.FindAsync(dto.DisplayModeId.Value);
			if (dm == null)
				return BadRequest(new ErrorResponseDto { Error = "Display mode not found" });
			entity.DisplayModeId = dto.DisplayModeId.Value;
		}

		if (dto.X.HasValue) entity.X = dto.X.Value;
		if (dto.Y.HasValue) entity.Y = dto.Y.Value;
		if (dto.W.HasValue) entity.W = dto.W.Value;
		if (dto.H.HasValue) entity.H = dto.H.Value;
		if (dto.MinW.HasValue) entity.MinW = dto.MinW.Value;
		if (dto.MinH.HasValue) entity.MinH = dto.MinH.Value;
		if (dto.Label != null) entity.Label = dto.Label;
		if (dto.Title != null) entity.Title = dto.Title;
		if (dto.Icon != null) entity.Icon = dto.Icon;

		entity.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		_logger.LogInformation("Updated PageComponent {Id}", id);
		return Ok(SuccessResultDto.True);
	}

	/// <summary>DELETE /api/pagecomponents/{id} - Delete a component</summary>
	[HttpDelete("{id:int}")]
	[ProducesResponseType(typeof(SuccessResultDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> Delete(int id)
	{
		var entity = await _context.PageComponents.FindAsync(id);
		if (entity == null)
			return NotFound(new ErrorResponseDto { Error = "Page component not found" });

		_context.PageComponents.Remove(entity);
		await _context.SaveChangesAsync();

		_logger.LogInformation("Deleted PageComponent {Id} from Page {PageId}", id, entity.PageId);
		return Ok(SuccessResultDto.True);
	}
}
