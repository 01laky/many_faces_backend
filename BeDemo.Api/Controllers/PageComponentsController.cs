using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

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
    public async Task<IActionResult> GetByPage(int pageId)
    {
        var components = await _context.PageComponents
            .Where(pc => pc.PageId == pageId)
            .Include(pc => pc.ComponentType)
            .Include(pc => pc.DisplayMode)
            .OrderBy(pc => pc.Y).ThenBy(pc => pc.X)
            .Select(pc => new
            {
                id = pc.Id,
                pageId = pc.PageId,
                gridKey = pc.GridKey,
                componentType = new
                {
                    id = pc.ComponentType.Id,
                    index = pc.ComponentType.Index,
                    name = pc.ComponentType.Name,
                },
                displayMode = new
                {
                    id = pc.DisplayMode.Id,
                    index = pc.DisplayMode.Index,
                    name = pc.DisplayMode.Name,
                },
                x = pc.X,
                y = pc.Y,
                w = pc.W,
                h = pc.H,
                minW = pc.MinW,
                minH = pc.MinH,
                label = pc.Label,
                title = pc.Title,
                icon = pc.Icon,
                createdAt = pc.CreatedAt,
            })
            .ToListAsync();

        return Ok(components);
    }

    /// <summary>GET /api/pagecomponents/{id} - Get a single component</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var pc = await _context.PageComponents
            .Include(p => p.ComponentType)
            .Include(p => p.DisplayMode)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pc == null)
            return NotFound(new { error = "Page component not found" });

        return Ok(new
        {
            id = pc.Id,
            pageId = pc.PageId,
            gridKey = pc.GridKey,
            componentType = new
            {
                id = pc.ComponentType.Id,
                index = pc.ComponentType.Index,
                name = pc.ComponentType.Name,
            },
            displayMode = new
            {
                id = pc.DisplayMode.Id,
                index = pc.DisplayMode.Index,
                name = pc.DisplayMode.Name,
            },
            x = pc.X,
            y = pc.Y,
            w = pc.W,
            h = pc.H,
            minW = pc.MinW,
            minH = pc.MinH,
            label = pc.Label,
            title = pc.Title,
            icon = pc.Icon,
            createdAt = pc.CreatedAt,
        });
    }

    /// <summary>POST /api/pagecomponents - Create a new component on a page</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePageComponentDto dto)
    {
        if (dto.PageId <= 0 || dto.ComponentTypeId <= 0 || dto.DisplayModeId <= 0)
            return BadRequest(new { error = "Invalid input" });

        var page = await _context.Pages.FindAsync(dto.PageId);
        if (page == null)
            return BadRequest(new { error = "Page not found" });

        var componentType = await _context.ComponentTypes.FindAsync(dto.ComponentTypeId);
        if (componentType == null)
            return BadRequest(new { error = "Component type not found" });

        var displayMode = await _context.DisplayModes.FindAsync(dto.DisplayModeId);
        if (displayMode == null)
            return BadRequest(new { error = "Display mode not found" });

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

        return Ok(new { id = entity.Id, gridKey });
    }

    /// <summary>PUT /api/pagecomponents/{id} - Update a component</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePageComponentDto dto)
    {
        var entity = await _context.PageComponents.FindAsync(id);
        if (entity == null)
            return NotFound(new { error = "Page component not found" });

        if (dto.ComponentTypeId.HasValue)
        {
            var ct = await _context.ComponentTypes.FindAsync(dto.ComponentTypeId.Value);
            if (ct == null)
                return BadRequest(new { error = "Component type not found" });
            entity.ComponentTypeId = dto.ComponentTypeId.Value;
        }

        if (dto.DisplayModeId.HasValue)
        {
            var dm = await _context.DisplayModes.FindAsync(dto.DisplayModeId.Value);
            if (dm == null)
                return BadRequest(new { error = "Display mode not found" });
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
        return Ok(new { success = true });
    }

    /// <summary>DELETE /api/pagecomponents/{id} - Delete a component</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _context.PageComponents.FindAsync(id);
        if (entity == null)
            return NotFound(new { error = "Page component not found" });

        _context.PageComponents.Remove(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted PageComponent {Id} from Page {PageId}", id, entity.PageId);
        return Ok(new { success = true });
    }
}

public class CreatePageComponentDto
{
    public int PageId { get; set; }
    public int ComponentTypeId { get; set; }
    public int DisplayModeId { get; set; }
    public string? GridKey { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int MinW { get; set; }
    public int MinH { get; set; }
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Icon { get; set; }
}

public class UpdatePageComponentDto
{
    public int? ComponentTypeId { get; set; }
    public int? DisplayModeId { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? W { get; set; }
    public int? H { get; set; }
    public int? MinW { get; set; }
    public int? MinH { get; set; }
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string? Icon { get; set; }
}
