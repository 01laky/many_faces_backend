using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;

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
    public async Task<IActionResult> GetComponentTypes()
    {
        var componentTypes = await _context.ComponentTypes
            .OrderBy(ct => ct.Id)
            .Select(ct => new
            {
                id = ct.Id,
                index = ct.Index,
                name = ct.Name,
                createdAt = ct.CreatedAt,
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} component types", componentTypes.Count);
        return Ok(componentTypes);
    }

    /// <summary>GET /api/componenttypes/{id} - Get component type by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetComponentType(int id)
    {
        var ct = await _context.ComponentTypes.FindAsync(id);
        if (ct == null)
            return NotFound(new { error = "Component type not found" });

        return Ok(new
        {
            id = ct.Id,
            index = ct.Index,
            name = ct.Name,
            createdAt = ct.CreatedAt,
        });
    }
}
