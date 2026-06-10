using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PageTypesController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<PageTypesController> _logger;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;

	public PageTypesController(
		ApplicationDbContext context,
		ILogger<PageTypesController> logger,
		IFaceScopeContext faceScope,
		IAccessEvaluator access)
	{
		_context = context;
		_logger = logger;
		_faceScope = faceScope;
		_access = access;
	}

	private bool CanMutateGlobalPageTypes() => _access.CanMutateGlobalPageTypes(User);

	/// <summary>
	/// GET /api/pagetypes
	/// Get list of all page types
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<PageTypeDetailDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetPageTypes()
	{
		try
		{
			var pageTypes = await _context.PageTypes
				.OrderBy(pt => pt.Index)
				.ToListAsync();

			var pageTypeDtos = pageTypes.Select(pt => new PageTypeDetailDto
			{
				Id = pt.Id,
				Index = pt.Index,
				CreatedAt = pt.CreatedAt,
				UpdatedAt = pt.UpdatedAt,
			}).ToList();

			_logger.LogInformation("Retrieved {Count} page types", pageTypeDtos.Count);
			return Ok(pageTypeDtos);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving page types");
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while retrieving page types" });
		}
	}

	/// <summary>
	/// GET /api/pagetypes/{id}
	/// Get page type by ID
	/// </summary>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(PageTypeDetailDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetPageType(int id)
	{
		try
		{
			var pageType = await _context.PageTypes.FindAsync(id);

			if (pageType == null)
			{
				_logger.LogWarning("PageType not found: {PageTypeId}", id);
				return NotFound(new ErrorResponseDto { Error = "PageType not found" });
			}

			var pageTypeDto = new PageTypeDetailDto
			{
				Id = pageType.Id,
				Index = pageType.Index,
				CreatedAt = pageType.CreatedAt,
				UpdatedAt = pageType.UpdatedAt,
			};

			_logger.LogInformation("Retrieved page type: {PageTypeId}", id);
			return Ok(pageTypeDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving page type: {PageTypeId}", id);
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while retrieving page type" });
		}
	}

	/// <summary>
	/// POST /api/pagetypes
	/// Create a new page type
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(PageTypeDetailDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreatePageType([FromBody] CreatePageTypeModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		if (!CanMutateGlobalPageTypes())
			return Forbid();

		try
		{
			var pageType = new PageType
			{
				Index = model.Index,
				CreatedAt = DateTime.UtcNow,
			};

			_context.PageTypes.Add(pageType);
			await _context.SaveChangesAsync();

			var pageTypeDto = new PageTypeDetailDto
			{
				Id = pageType.Id,
				Index = pageType.Index,
				CreatedAt = pageType.CreatedAt,
				UpdatedAt = pageType.UpdatedAt,
			};

			_logger.LogInformation("PageType created: {PageTypeId}", pageType.Id);
			var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
			SecurityAuditLog.GlobalPageTypeMutation(_logger, actor, "create", pageType.Id, HttpContext.TraceIdentifier);
			return CreatedAtAction(nameof(GetPageType), new { id = pageType.Id }, pageTypeDto);
		}
		catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint") == true)
		{
			_logger.LogWarning("PageType with index '{Index}' already exists", model.Index);
			return BadRequest(new ErrorResponseDto { Error = $"PageType with index '{model.Index}' already exists" });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating page type");
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while creating page type" });
		}
	}

	/// <summary>
	/// PUT /api/pagetypes/{id}
	/// Update page type by ID
	/// </summary>
	[HttpPut("{id}")]
	[ProducesResponseType(typeof(PageTypeDetailDto), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdatePageType(int id, [FromBody] UpdatePageTypeModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		if (!CanMutateGlobalPageTypes())
			return Forbid();

		try
		{
			var pageType = await _context.PageTypes.FindAsync(id);

			if (pageType == null)
			{
				_logger.LogWarning("PageType not found for update: {PageTypeId}", id);
				return NotFound(new ErrorResponseDto { Error = "PageType not found" });
			}

			// Update page type properties
			if (model.Index != null)
			{
				pageType.Index = model.Index;
			}
			pageType.UpdatedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();

			var pageTypeDto = new PageTypeDetailDto
			{
				Id = pageType.Id,
				Index = pageType.Index,
				CreatedAt = pageType.CreatedAt,
				UpdatedAt = pageType.UpdatedAt,
			};

			_logger.LogInformation("PageType updated: {PageTypeId}", id);
			var actorPut = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
			SecurityAuditLog.GlobalPageTypeMutation(_logger, actorPut, "update", id, HttpContext.TraceIdentifier);
			return Ok(pageTypeDto);
		}
		catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint") == true)
		{
			_logger.LogWarning("PageType with index '{Index}' already exists", model.Index);
			return BadRequest(new ErrorResponseDto { Error = $"PageType with index '{model.Index}' already exists" });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating page type: {PageTypeId}", id);
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while updating page type" });
		}
	}

	/// <summary>
	/// DELETE /api/pagetypes/{id}
	/// Delete page type by ID
	/// </summary>
	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeletePageType(int id)
	{
		if (!CanMutateGlobalPageTypes())
			return Forbid();

		try
		{
			var pageType = await _context.PageTypes.FindAsync(id);
			if (pageType == null)
			{
				_logger.LogWarning("PageType not found for deletion: {PageTypeId}", id);
				return NotFound(new ErrorResponseDto { Error = "PageType not found" });
			}

			// Check if any pages use this page type
			var pagesUsingType = await _context.Pages.AnyAsync(p => p.PageTypeId == id);
			if (pagesUsingType)
			{
				_logger.LogWarning("Cannot delete PageType {PageTypeId} because it is used by pages", id);
				return BadRequest(new ErrorResponseDto { Error = "Cannot delete PageType because it is used by pages" });
			}

			_context.PageTypes.Remove(pageType);
			await _context.SaveChangesAsync();

			_logger.LogInformation("PageType deleted: {PageTypeId}", id);
			var actorDel = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
			SecurityAuditLog.GlobalPageTypeMutation(_logger, actorDel, "delete", id, HttpContext.TraceIdentifier);
			return NoContent();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting page type: {PageTypeId}", id);
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while deleting page type" });
		}
	}
}
