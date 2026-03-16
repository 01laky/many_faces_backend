using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FacesController> _logger;

    public FacesController(
        ApplicationDbContext context,
        ILogger<FacesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/faces
    /// Get list of all faces
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFaces()
    {
        try
        {
            var faces = await _context.Faces
                .OrderBy(f => f.Index)
                .ToListAsync();

            var faceDtos = faces.Select(f => new
            {
                id = f.Id,
                index = f.Index,
                title = f.Title,
                description = f.Description,
                color = f.Color,
                gradientSettings = f.GradientSettings,
                isPublic = f.IsPublic,
                createdAt = f.CreatedAt,
                updatedAt = f.UpdatedAt,
            }).ToList();

            _logger.LogInformation("Retrieved {Count} faces", faceDtos.Count);
            return Ok(faceDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving faces");
            return StatusCode(500, new { error = "An error occurred while retrieving faces" });
        }
    }

    /// <summary>
    /// GET /api/faces-config
    /// Get all faces with their pages configuration
    /// - For authenticated users: returns only private faces (IsPublic = false)
    /// - For anonymous users: returns only public faces (IsPublic = true)
    /// Used by frontend to generate routes before router initialization
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFacesConfig()
    {
        try
        {
            // Return ALL faces with pages, route translations and isPublic flag.
            // The frontend decides which faces to show based on auth state.
            var faces = await _context.Faces
                .Include(f => f.Pages)
                    .ThenInclude(p => p.PageType)
                .Include(f => f.Pages)
                    .ThenInclude(p => p.RouteTranslations)
                .OrderBy(f => f.Index)
                .ToListAsync();

            var facesConfig = faces.Select(f => new
            {
                index = f.Index,
                id = f.Id,
                title = f.Title,
                description = f.Description,
                color = f.Color,
                gradientSettings = f.GradientSettings,
                isPublic = f.IsPublic,
                pages = f.Pages
                    .OrderBy(p => p.Index)
                    .Select(p => new
                    {
                        index = p.Index,
                        id = p.Id,
                        name = p.Name,
                        description = p.Description,
                        path = p.Path,
                        gridSchema = p.GridSchema,
                        pageType = new
                        {
                            index = p.PageType.Index,
                            id = p.PageType.Id
                        },
                        routeTranslations = p.RouteTranslations
                            .OrderBy(rt => rt.LanguageCode)
                            .Select(rt => new
                            {
                                languageCode = rt.LanguageCode,
                                translatedRoute = rt.TranslatedRoute,
                            }).ToList(),
                        createdAt = p.CreatedAt,
                        updatedAt = p.UpdatedAt
                    }).ToList()
            }).ToList();

            _logger.LogInformation("Retrieved faces config with {FaceCount} faces", facesConfig.Count);
            return Ok(facesConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving faces config");
            return StatusCode(500, new { error = "An error occurred while retrieving faces config" });
        }
    }

    /// <summary>
    /// GET /api/faces/{id}
    /// Get face by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFace(int id)
    {
        try
        {
            var face = await _context.Faces.FindAsync(id);

            if (face == null)
            {
                _logger.LogWarning("Face not found: {FaceId}", id);
                return NotFound(new { error = "Face not found" });
            }

            var faceDto = new
            {
                id = face.Id,
                index = face.Index,
                title = face.Title,
                description = face.Description,
                color = face.Color,
                gradientSettings = face.GradientSettings,
                isPublic = face.IsPublic,
                createdAt = face.CreatedAt,
                updatedAt = face.UpdatedAt,
            };

            _logger.LogInformation("Retrieved face: {FaceId}", id);
            return Ok(faceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving face: {FaceId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving face" });
        }
    }

    /// <summary>
    /// POST /api/faces
    /// Create a new face
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateFace([FromBody] CreateFaceModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Check if index already exists
            var existingFace = await _context.Faces.FirstOrDefaultAsync(f => f.Index == model.Index);
            if (existingFace != null)
            {
                _logger.LogWarning("Face with index already exists: {Index}", model.Index);
                return BadRequest(new { error = "Face with this index already exists" });
            }

            var face = new Face
            {
                Index = model.Index,
                Title = model.Title,
                Description = model.Description,
                Color = model.Color,
                GradientSettings = model.GradientSettings,
                IsPublic = model.IsPublic,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Faces.Add(face);
            await _context.SaveChangesAsync();

            // Add default pages (Home, List, Detail; Wall for non-public faces) when PageTypes exist
            var homePageType = await _context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "home");
            var listPageType = await _context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "list");
            var detailPageType = await _context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "detail");
            var wallPageType = await _context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "wall");

            var defaultPages = new List<Page>();
            var pageIndex = 0;
            if (homePageType != null)
            {
                defaultPages.Add(new Page { FaceId = face.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = pageIndex++, CreatedAt = DateTime.UtcNow });
            }

            if (listPageType != null)
            {
                defaultPages.Add(new Page { FaceId = face.Id, PageTypeId = listPageType.Id, Name = "List", Path = "/list", Index = pageIndex++, CreatedAt = DateTime.UtcNow });
            }

            if (detailPageType != null)
            {
                defaultPages.Add(new Page { FaceId = face.Id, PageTypeId = detailPageType.Id, Name = "Detail", Path = "/detail", Index = pageIndex++, CreatedAt = DateTime.UtcNow });
            }

            if (!face.IsPublic && wallPageType != null)
            {
                defaultPages.Add(new Page { FaceId = face.Id, PageTypeId = wallPageType.Id, Name = "Wall", Path = "/wall", Index = pageIndex++, CreatedAt = DateTime.UtcNow });
            }

            if (defaultPages.Count > 0)
            {
                _context.Pages.AddRange(defaultPages);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Face created with {Count} default pages: {FaceId}", defaultPages.Count, face.Id);
            }
            else
            {
                _logger.LogInformation("Face created: {FaceId} (no default PageTypes found)", face.Id);
            }

            var faceDto = new
            {
                id = face.Id,
                index = face.Index,
                title = face.Title,
                description = face.Description,
                color = face.Color,
                gradientSettings = face.GradientSettings,
                isPublic = face.IsPublic,
                createdAt = face.CreatedAt,
                updatedAt = face.UpdatedAt,
            };

            return CreatedAtAction(nameof(GetFace), new { id = face.Id }, faceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating face");
            return StatusCode(500, new { error = "An error occurred while creating face" });
        }
    }

    /// <summary>
    /// PUT /api/faces/{id}
    /// Update face by ID
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFace(int id, [FromBody] UpdateFaceModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var face = await _context.Faces.FindAsync(id);

            if (face == null)
            {
                _logger.LogWarning("Face not found for update: {FaceId}", id);
                return NotFound(new { error = "Face not found" });
            }

            // Check if index already exists (excluding current face)
            if (model.Index != null && model.Index != face.Index)
            {
                var existingFace = await _context.Faces.FirstOrDefaultAsync(f => f.Index == model.Index && f.Id != id);
                if (existingFace != null)
                {
                    _logger.LogWarning("Face with index already exists: {Index}", model.Index);
                    return BadRequest(new { error = "Face with this index already exists" });
                }
            }

            // Update face properties
            if (model.Index != null)
            {
                face.Index = model.Index;
            }
            if (model.Title != null)
            {
                face.Title = model.Title;
            }
            if (model.Description != null)
            {
                face.Description = model.Description;
            }
            if (model.Color != null)
            {
                face.Color = model.Color;
            }
            if (model.GradientSettings != null)
            {
                face.GradientSettings = model.GradientSettings;
            }
            if (model.IsPublic.HasValue)
            {
                face.IsPublic = model.IsPublic.Value;
            }
            face.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var faceDto = new
            {
                id = face.Id,
                index = face.Index,
                title = face.Title,
                description = face.Description,
                color = face.Color,
                gradientSettings = face.GradientSettings,
                isPublic = face.IsPublic,
                createdAt = face.CreatedAt,
                updatedAt = face.UpdatedAt,
            };

            _logger.LogInformation("Face updated: {FaceId}", id);
            return Ok(faceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating face: {FaceId}", id);
            return StatusCode(500, new { error = "An error occurred while updating face" });
        }
    }

    /// <summary>
    /// DELETE /api/faces/{id}
    /// Delete face by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFace(int id)
    {
        try
        {
            var face = await _context.Faces.FindAsync(id);

            if (face == null)
            {
                _logger.LogWarning("Face not found for deletion: {FaceId}", id);
                return NotFound(new { error = "Face not found" });
            }

            _context.Faces.Remove(face);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Face deleted: {FaceId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting face: {FaceId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting face" });
        }
    }
}

/// <summary>
/// Model for creating a new face
/// </summary>
public class CreateFaceModel
{
    [Required(ErrorMessage = "Index is required")]
    [StringLength(100, ErrorMessage = "Index must be at most 100 characters")]
    public string Index { get; set; } = string.Empty;

    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, ErrorMessage = "Title must be at most 200 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "Color must be at most 50 characters")]
    public string? Color { get; set; }

    public string? GradientSettings { get; set; }

    public bool IsPublic { get; set; } = true;
}

/// <summary>
/// Model for updating a face
/// </summary>
public class UpdateFaceModel
{
    [StringLength(100, ErrorMessage = "Index must be at most 100 characters")]
    public string? Index { get; set; }

    [StringLength(200, ErrorMessage = "Title must be at most 200 characters")]
    public string? Title { get; set; }

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters")]
    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "Color must be at most 50 characters")]
    public string? Color { get; set; }

    public string? GradientSettings { get; set; }

    public bool? IsPublic { get; set; }
}
