using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;

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
                visibility = f.Visibility.ToString(),
                allowRecensions = f.AllowRecensions,
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
            var faces = await _context.Faces
                .Include(f => f.Pages)
                    .ThenInclude(p => p.PageType)
                .Include(f => f.Pages)
                    .ThenInclude(p => p.RouteTranslations)
                .OrderBy(f => f.Index)
                .ToListAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Dictionary<int, (int RoleId, string RoleName)>? myFaceRoles = null;
            Dictionary<int, (bool Visited, bool FaceRoleIntroCompleted)>? myFaceState = null;
            if (!string.IsNullOrEmpty(userId))
            {
                var userFaceRoles = await _context.UserFaceRoles
                    .Where(ufr => ufr.UserId == userId)
                    .Include(ufr => ufr.UserRole)
                    .ToListAsync();
                myFaceRoles = userFaceRoles
                    .Where(ufr => ufr.UserRole != null)
                    .ToDictionary(ufr => ufr.FaceId, ufr => (ufr.UserRoleId, ufr.UserRole!.Name));

                var userProfileId = await _context.UserProfiles
                    .AsNoTracking()
                    .Where(up => up.UserId == userId)
                    .Select(up => up.Id)
                    .FirstOrDefaultAsync();
                if (userProfileId != 0)
                {
                    var ufRows = await _context.UserFaceProfiles
                        .AsNoTracking()
                        .Where(ufp => ufp.UserProfileId == userProfileId)
                        .Select(ufp => new { ufp.FaceId, ufp.Visited, ufp.FaceRoleIntroCompleted })
                        .ToListAsync();
                    myFaceState = ufRows.ToDictionary(x => x.FaceId, x => (x.Visited, x.FaceRoleIntroCompleted));
                }
            }

            var facesConfig = faces.Select(f =>
            {
                object? myFaceRoleId = null;
                object? myFaceRoleName = null;
                if (myFaceRoles != null && myFaceRoles.TryGetValue(f.Id, out var role))
                {
                    myFaceRoleId = role.RoleId;
                    myFaceRoleName = role.RoleName;
                }

                object? myVisited = null;
                object? myFaceRoleIntroCompleted = null;
                if (myFaceState != null && myFaceState.TryGetValue(f.Id, out var st))
                {
                    myVisited = st.Visited;
                    myFaceRoleIntroCompleted = st.FaceRoleIntroCompleted;
                }

                return new
                {
                    index = f.Index,
                    id = f.Id,
                    title = f.Title,
                    description = f.Description,
                    color = f.Color,
                    gradientSettings = f.GradientSettings,
                    isPublic = f.IsPublic,
                    visibility = f.Visibility.ToString(),
                    allowRecensions = f.AllowRecensions,
                    myFaceRoleId,
                    myFaceRoleName,
                    myVisited,
                    myFaceRoleIntroCompleted,
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
                            pageType = p.PageType != null
                                ? new { index = p.PageType.Index, id = p.PageType.Id }
                                : (object?)null,
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
                };
            }).ToList();

            _logger.LogInformation("Retrieved faces config with {FaceCount} faces", facesConfig.Count);
            return Ok(facesConfig);
        }
        catch (PostgresException ex) when (ex.SqlState == "28000" && ex.MessageText?.Contains("does not exist", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning(
                "Database role from connection string does not exist in PostgreSQL. " +
                "Returning empty config so the app can load. Message: {Message}",
                ex.MessageText);
            return Ok(Array.Empty<object>());
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            _logger.LogWarning(
                ex,
                "Table(s) do not exist yet (migrations not applied or DB empty). Returning empty faces config. Message: {Message}",
                ex.MessageText);
            return Ok(Array.Empty<object>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving faces config");
            return StatusCode(500, new { error = "An error occurred while retrieving faces config" });
        }
    }

    /// <summary>
    /// GET /api/faces/face-roles
    /// Returns list of face-scoped roles (for role selector on first visit to a private face).
    /// </summary>
    [HttpGet("face-roles")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFaceRoles()
    {
        try
        {
            var roles = await _context.UserRoles
                .Where(r => r.Scope == RoleScope.Face)
                .OrderBy(r => r.Name)
                .Select(r => new { id = r.Id, name = r.Name })
                .ToListAsync();
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving face roles");
            return StatusCode(500, new { error = "An error occurred while retrieving face roles" });
        }
    }

    /// <summary>
    /// PUT /api/faces/{id}/my-role
    /// Set current user's face role for the given face. Used when user selects role on first visit to a private face.
    /// </summary>
    [HttpPut("{id}/my-role")]
    public async Task<IActionResult> SetMyFaceRole(int id, [FromBody] SetMyFaceRoleModel model)
    {
        if (model == null || model.UserRoleId <= 0)
        {
            return BadRequest(new { error = "userRoleId is required" });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var face = await _context.Faces.FindAsync(id);
            if (face == null)
            {
                return NotFound(new { error = "Face not found" });
            }

            var role = await _context.UserRoles.FindAsync(model.UserRoleId);
            if (role == null || role.Scope != RoleScope.Face)
            {
                return BadRequest(new { error = "Invalid face role" });
            }

            var existing = await _context.UserFaceRoles
                .FirstOrDefaultAsync(ufr => ufr.UserId == userId && ufr.FaceId == id);

            if (existing != null)
            {
                existing.UserRoleId = model.UserRoleId;
                _context.UserFaceRoles.Update(existing);
            }
            else
            {
                _context.UserFaceRoles.Add(new UserFaceRole
                {
                    UserId = userId,
                    FaceId = id,
                    UserRoleId = model.UserRoleId,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);
            if (userProfile != null)
            {
                var ufp = await _context.UserFaceProfiles
                    .FirstOrDefaultAsync(x => x.UserProfileId == userProfile.Id && x.FaceId == id);
                if (ufp != null)
                {
                    ufp.FaceRoleIntroCompleted = true;
                    ufp.IsActive = FaceRoleParticipation.IsActiveForFaceRoleName(role.Name);
                    ufp.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} set face role to {RoleName} for face {FaceId}", userId, role.Name, id);
            return Ok(new { userRoleId = model.UserRoleId, userRoleName = role.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting face role for face {FaceId}", id);
            return StatusCode(500, new { error = "An error occurred while setting face role" });
        }
    }

    /// <summary>
    /// POST /api/faces/{id}/visit — mark current user as having switched to this face (Visited = true).
    /// </summary>
    [HttpPost("{id:int}/visit")]
    public async Task<IActionResult> MarkFaceVisited(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var face = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (face == null)
            return NotFound(new { error = "Face not found" });

        var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);
        if (userProfile == null)
            return BadRequest(new { error = "User profile not found" });

        var ufp = await _context.UserFaceProfiles
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfile.Id && x.FaceId == id);
        if (ufp == null)
        {
            ufp = new UserFaceProfile
            {
                UserProfileId = userProfile.Id,
                FaceId = id,
                IsActive = false,
                Visited = true,
                FaceRoleIntroCompleted = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.UserFaceProfiles.Add(ufp);
        }
        else
        {
            ufp.Visited = true;
            ufp.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { visited = true });
    }

    /// <summary>
    /// POST /api/faces/{id}/exit-face — leave non-host participation; purge face-scoped social data and reset role to FACE_HOST.
    /// </summary>
    [HttpPost("{id:int}/exit-face")]
    public async Task<IActionResult> ExitFace(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var face = await _context.Faces.FirstOrDefaultAsync(f => f.Id == id);
        if (face == null)
            return NotFound(new { error = "Face not found" });

        var hostRole = await _context.UserRoles
            .FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost && r.Scope == RoleScope.Face);
        if (hostRole == null)
            return StatusCode(500, new { error = "FACE_HOST role missing" });

        var ufr = await _context.UserFaceRoles
            .Include(x => x.UserRole)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.FaceId == id);
        if (ufr?.UserRole == null)
            return BadRequest(new { error = "No face role for this face" });
        if (FaceRoleParticipation.IsHostFaceRole(ufr.UserRole.Name))
            return BadRequest(new { error = "Already in host role" });

        var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);
        if (userProfile == null)
            return BadRequest(new { error = "User profile not found" });

        var myUfp = await _context.UserFaceProfiles
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfile.Id && x.FaceId == id);
        if (myUfp == null)
            return BadRequest(new { error = "Face profile not found" });

        var faceProfileIds = await _context.UserFaceProfiles
            .Where(x => x.FaceId == id)
            .Select(x => x.Id)
            .ToListAsync();

        // Likes: on my profile, or given by me on any profile in this face
        var likesToRemove = await _context.UserFaceProfileLikes
            .Where(l => faceProfileIds.Contains(l.UserFaceProfileId) &&
                        (l.UserFaceProfileId == myUfp.Id || l.UserId == userId))
            .ToListAsync();
        _context.UserFaceProfileLikes.RemoveRange(likesToRemove);

        var commentsToRemove = await _context.UserFaceProfileComments
            .Where(c => faceProfileIds.Contains(c.UserFaceProfileId) &&
                        (c.UserFaceProfileId == myUfp.Id || c.UserId == userId))
            .ToListAsync();
        _context.UserFaceProfileComments.RemoveRange(commentsToRemove);

        var reviewsToRemove = await _context.UserFaceProfileReviews
            .Where(r => faceProfileIds.Contains(r.UserFaceProfileId) &&
                        (r.UserFaceProfileId == myUfp.Id || r.AuthorUserId == userId))
            .ToListAsync();
        _context.UserFaceProfileReviews.RemoveRange(reviewsToRemove);

        myUfp.DisplayName = null;
        myUfp.AvatarUrl = null;
        myUfp.Settings = null;
        myUfp.IsActive = false;
        myUfp.FaceRoleIntroCompleted = true;
        myUfp.Visited = true;
        myUfp.UpdatedAt = DateTime.UtcNow;

        ufr.UserRoleId = hostRole.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} exited face {FaceId} (reset to FACE_HOST)", userId, id);
        return Ok(new { message = "Exited face", userRoleId = hostRole.Id, userRoleName = hostRole.Name });
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
                visibility = face.Visibility.ToString(),
                allowRecensions = face.AllowRecensions,
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
                Visibility = model.Visibility ?? FaceVisibility.Public,
                AllowRecensions = model.AllowRecensions ?? false,
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
                visibility = face.Visibility.ToString(),
                allowRecensions = face.AllowRecensions,
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
            if (model.Visibility.HasValue)
            {
                face.Visibility = model.Visibility.Value;
            }
            if (model.AllowRecensions.HasValue)
            {
                face.AllowRecensions = model.AllowRecensions.Value;
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
                visibility = face.Visibility.ToString(),
                allowRecensions = face.AllowRecensions,
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

    public FaceVisibility? Visibility { get; set; }

    public bool? AllowRecensions { get; set; }
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

    public FaceVisibility? Visibility { get; set; }

    public bool? AllowRecensions { get; set; }
}

/// <summary>
/// Model for setting current user's face role (PUT /api/faces/{id}/my-role)
/// </summary>
public class SetMyFaceRoleModel
{
    public int UserRoleId { get; set; }
}
