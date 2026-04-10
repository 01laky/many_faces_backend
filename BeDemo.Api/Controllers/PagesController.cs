using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PagesController> _logger;
    private readonly IFaceScopeContext _faceScope;

    public PagesController(
        ApplicationDbContext context,
        ILogger<PagesController> logger,
        IFaceScopeContext faceScope)
    {
        _context = context;
        _logger = logger;
        _faceScope = faceScope;
    }

    /// <summary>Admin SPA (/admin/) with global Admin JWT may see or move pages across faces.</summary>
    private bool CanManageAllFaces() =>
        _faceScope.IsAdminFaceScope &&
        (User.IsInRole(UserRole.GlobalRoleNames.Admin) ||
         User.IsInRole(UserRole.GlobalRoleNames.SuperAdmin));

    /// <summary>Returns NotFound when a tenant tries to touch another face's page.</summary>
    private IActionResult? EnsurePageBelongsToScope(Page page)
    {
        if (CanManageAllFaces())
            return null;
        if (page.FaceId != _faceScope.FaceId)
            return NotFound(new { error = "Page not found" });
        return null;
    }

    /// <summary>
    /// GET /api/pages?faceId={faceId}
    /// Get list of pages for a specific face
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPages([FromQuery] int? faceId)
    {
        try
        {
            IQueryable<Page> query = _context.Pages;

            if (CanManageAllFaces())
            {
                // Optional filter when admin passes ?faceId= for a specific tenant.
                if (faceId.HasValue)
                    query = query.Where(p => p.FaceId == faceId.Value);
            }
            else
            {
                // Tenants always see only their scoped face's pages (ignore spoofed query).
                query = query.Where(p => p.FaceId == _faceScope.FaceId);
            }

            var pages = await query
                .OrderBy(p => p.Index)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var pageDtos = pages.Select(p => new
            {
                id = p.Id,
                faceId = p.FaceId,
                pageTypeId = p.PageTypeId,
                name = p.Name,
                description = p.Description,
                path = p.Path,
                index = p.Index,
                gridSchema = p.GridSchema,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
            }).ToList();

            _logger.LogInformation("Retrieved {Count} pages", pageDtos.Count);
            return Ok(pageDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pages");
            return StatusCode(500, new { error = "An error occurred while retrieving pages" });
        }
    }

    /// <summary>
    /// GET /api/pages/{id}
    /// Get page by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPage(int id)
    {
        try
        {
            var page = await _context.Pages.FindAsync(id);

            if (page == null)
            {
                _logger.LogWarning("Page not found: {PageId}", id);
                return NotFound(new { error = "Page not found" });
            }

            var gate = EnsurePageBelongsToScope(page);
            if (gate != null)
                return gate;

            var pageDto = new
            {
                id = page.Id,
                faceId = page.FaceId,
                pageTypeId = page.PageTypeId,
                name = page.Name,
                description = page.Description,
                path = page.Path,
                index = page.Index,
                gridSchema = page.GridSchema,
                createdAt = page.CreatedAt,
                updatedAt = page.UpdatedAt,
            };

            _logger.LogInformation("Retrieved page: {PageId}", id);
            return Ok(pageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving page: {PageId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving page" });
        }
    }

    /// <summary>
    /// POST /api/pages
    /// Create a new page
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePage([FromBody] CreatePageModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Verify that Face exists
            var faceExists = await _context.Faces.AnyAsync(f => f.Id == model.FaceId);
            if (!faceExists)
            {
                _logger.LogWarning("Face not found: {FaceId}", model.FaceId);
                return BadRequest(new { error = "Face not found" });
            }

            if (!CanManageAllFaces() && model.FaceId != _faceScope.FaceId)
                return BadRequest(new { error = "Pages can only be created for the current face scope" });

            // Verify that PageType exists
            var pageTypeExists = await _context.PageTypes.AnyAsync(pt => pt.Id == model.PageTypeId);
            if (!pageTypeExists)
            {
                _logger.LogWarning("PageType not found: {PageTypeId}", model.PageTypeId);
                return BadRequest(new { error = "PageType not found" });
            }

            var page = new Page
            {
                FaceId = model.FaceId,
                PageTypeId = model.PageTypeId,
                Name = model.Name,
                Description = model.Description,
                Path = model.Path,
                Index = model.Index,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Pages.Add(page);
            await _context.SaveChangesAsync();

            var pageDto = new
            {
                id = page.Id,
                faceId = page.FaceId,
                pageTypeId = page.PageTypeId,
                name = page.Name,
                description = page.Description,
                path = page.Path,
                index = page.Index,
                createdAt = page.CreatedAt,
                updatedAt = page.UpdatedAt,
            };

            _logger.LogInformation("Page created: {PageId}", page.Id);
            return CreatedAtAction(nameof(GetPage), new { id = page.Id }, pageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating page");
            return StatusCode(500, new { error = "An error occurred while creating page" });
        }
    }

    /// <summary>
    /// PUT /api/pages/{id}
    /// Update page by ID
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePage(int id, [FromBody] UpdatePageModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var page = await _context.Pages.FindAsync(id);

            if (page == null)
            {
                _logger.LogWarning("Page not found for update: {PageId}", id);
                return NotFound(new { error = "Page not found" });
            }

            var updateGate = EnsurePageBelongsToScope(page);
            if (updateGate != null)
                return updateGate;

            // Update page properties
            if (model.Name != null)
            {
                page.Name = model.Name;
            }
            if (model.Description != null)
            {
                page.Description = model.Description;
            }
            if (model.Path != null)
            {
                page.Path = model.Path;
            }
            if (model.Index.HasValue)
            {
                page.Index = model.Index.Value;
            }
            if (model.GridSchema != null)
            {
                page.GridSchema = model.GridSchema;
            }
            if (model.FaceId.HasValue)
            {
                if (!CanManageAllFaces())
                    return BadRequest(new { error = "Only admin scope may reassign a page to another face" });

                // Verify that new Face exists
                var faceExists = await _context.Faces.AnyAsync(f => f.Id == model.FaceId.Value);
                if (!faceExists)
                {
                    _logger.LogWarning("Face not found: {FaceId}", model.FaceId.Value);
                    return BadRequest(new { error = "Face not found" });
                }
                page.FaceId = model.FaceId.Value;
            }
            if (model.PageTypeId.HasValue)
            {
                // Verify that new PageType exists
                var pageTypeExists = await _context.PageTypes.AnyAsync(pt => pt.Id == model.PageTypeId.Value);
                if (!pageTypeExists)
                {
                    _logger.LogWarning("PageType not found: {PageTypeId}", model.PageTypeId.Value);
                    return BadRequest(new { error = "PageType not found" });
                }
                page.PageTypeId = model.PageTypeId.Value;
            }
            page.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var pageDto = new
            {
                id = page.Id,
                faceId = page.FaceId,
                pageTypeId = page.PageTypeId,
                name = page.Name,
                description = page.Description,
                path = page.Path,
                index = page.Index,
                gridSchema = page.GridSchema,
                createdAt = page.CreatedAt,
                updatedAt = page.UpdatedAt,
            };

            _logger.LogInformation("Page updated: {PageId}", id);
            return Ok(pageDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating page: {PageId}", id);
            return StatusCode(500, new { error = "An error occurred while updating page" });
        }
    }

    /// <summary>
    /// DELETE /api/pages/{id}
    /// Delete page by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePage(int id)
    {
        try
        {
            var page = await _context.Pages.FindAsync(id);

            if (page == null)
            {
                _logger.LogWarning("Page not found for deletion: {PageId}", id);
                return NotFound(new { error = "Page not found" });
            }

            var delGate = EnsurePageBelongsToScope(page);
            if (delGate != null)
                return delGate;

            _context.Pages.Remove(page);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Page deleted: {PageId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting page: {PageId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting page" });
        }
    }

    /// <summary>
    /// GET /api/pages/{pageId}/translations
    /// Get all route translations for a page
    /// </summary>
    [HttpGet("{pageId}/translations")]
    public async Task<IActionResult> GetPageRouteTranslations(int pageId)
    {
        try
        {
            var page = await _context.Pages.FindAsync(pageId);
            if (page == null)
                return NotFound(new { error = "Page not found" });
            var trGate = EnsurePageBelongsToScope(page);
            if (trGate != null)
                return trGate;

            var translations = await _context.PageRouteTranslations
                .Where(t => t.PageId == pageId)
                .OrderBy(t => t.LanguageCode)
                .Select(t => new
                {
                    id = t.Id,
                    pageId = t.PageId,
                    languageCode = t.LanguageCode,
                    translatedRoute = t.TranslatedRoute,
                    createdAt = t.CreatedAt,
                    updatedAt = t.UpdatedAt,
                })
                .ToListAsync();

            return Ok(translations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving translations for page: {PageId}", pageId);
            return StatusCode(500, new { error = "An error occurred while retrieving page translations" });
        }
    }

    /// <summary>
    /// PUT /api/pages/{pageId}/translations
    /// Upsert route translations for a page (replaces all translations)
    /// </summary>
    [HttpPut("{pageId}/translations")]
    public async Task<IActionResult> UpdatePageRouteTranslations(int pageId, [FromBody] List<PageRouteTranslationModel> models)
    {
        try
        {
            var page = await _context.Pages.FindAsync(pageId);
            if (page == null)
                return NotFound(new { error = "Page not found" });
            var trGate = EnsurePageBelongsToScope(page);
            if (trGate != null)
                return trGate;

            // Get existing translations
            var existing = await _context.PageRouteTranslations
                .Where(t => t.PageId == pageId)
                .ToListAsync();

            // Remove translations not in the new set
            var newLanguageCodes = models.Select(m => m.LanguageCode).ToHashSet();
            var toRemove = existing.Where(e => !newLanguageCodes.Contains(e.LanguageCode)).ToList();
            _context.PageRouteTranslations.RemoveRange(toRemove);

            // Upsert translations
            foreach (var model in models)
            {
                var existingTranslation = existing.FirstOrDefault(e => e.LanguageCode == model.LanguageCode);
                if (existingTranslation != null)
                {
                    existingTranslation.TranslatedRoute = model.TranslatedRoute;
                    existingTranslation.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.PageRouteTranslations.Add(new PageRouteTranslation
                    {
                        PageId = pageId,
                        LanguageCode = model.LanguageCode,
                        TranslatedRoute = model.TranslatedRoute,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Return updated translations
            var translations = await _context.PageRouteTranslations
                .Where(t => t.PageId == pageId)
                .OrderBy(t => t.LanguageCode)
                .Select(t => new
                {
                    id = t.Id,
                    pageId = t.PageId,
                    languageCode = t.LanguageCode,
                    translatedRoute = t.TranslatedRoute,
                    createdAt = t.CreatedAt,
                    updatedAt = t.UpdatedAt,
                })
                .ToListAsync();

            _logger.LogInformation("Updated {Count} translations for page: {PageId}", translations.Count, pageId);
            return Ok(translations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating translations for page: {PageId}", pageId);
            return StatusCode(500, new { error = "An error occurred while updating page translations" });
        }
    }
}

/// <summary>
/// Model for creating a new page
/// </summary>
public class CreatePageModel
{
    [Required(ErrorMessage = "FaceId is required")]
    public int FaceId { get; set; }

    [Required(ErrorMessage = "PageTypeId is required")]
    public int PageTypeId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, ErrorMessage = "Name must be at most 200 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Path is required")]
    [StringLength(500, ErrorMessage = "Path must be at most 500 characters")]
    public string Path { get; set; } = string.Empty;

    public int Index { get; set; } = 0;
}

/// <summary>
/// Model for updating a page
/// </summary>
public class UpdatePageModel
{
    public int? FaceId { get; set; }

    public int? PageTypeId { get; set; }

    [StringLength(200, ErrorMessage = "Name must be at most 200 characters")]
    public string? Name { get; set; }

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "Path must be at most 500 characters")]
    public string? Path { get; set; }

    public int? Index { get; set; }

    public string? GridSchema { get; set; }
}

/// <summary>
/// Model for page route translation
/// </summary>
public class PageRouteTranslationModel
{
    [Required(ErrorMessage = "LanguageCode is required")]
    [StringLength(10, ErrorMessage = "LanguageCode must be at most 10 characters")]
    public string LanguageCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "TranslatedRoute is required")]
    [StringLength(200, ErrorMessage = "TranslatedRoute must be at most 200 characters")]
    public string TranslatedRoute { get; set; } = string.Empty;
}
