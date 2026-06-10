using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Faces;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.ProfileDetail;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacesController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<FacesController> _logger;
	private readonly IFaceScopeContext _faceScope;
	private readonly IMemoryCache _memoryCache;
	private readonly IAccessEvaluator _access;
	private readonly IProfileDetailTemplatePagesService _profileDetailTemplates;
	private readonly IFacesConfigService _facesConfig;

	public FacesController(
		ApplicationDbContext context,
		ILogger<FacesController> logger,
		IFaceScopeContext faceScope,
		IMemoryCache memoryCache,
		IAccessEvaluator access,
		IProfileDetailTemplatePagesService profileDetailTemplates,
		IFacesConfigService facesConfig)
	{
		_context = context;
		_logger = logger;
		_faceScope = faceScope;
		_memoryCache = memoryCache;
		_access = access;
		_profileDetailTemplates = profileDetailTemplates;
		_facesConfig = facesConfig;
	}

	private void InvalidateFacesRoutingCache()
	{
		_memoryCache.Remove("Faces");
		_facesConfig.InvalidateAll();
	}

	/// <summary>Global Admin or SuperAdmin — used for tenant-scoped elevation (e.g. public faces-config), not admin-face platform APIs.</summary>
	private bool IsGlobalAdmin() => _access.IsGlobalAdmin(User);

	/// <summary>Admin face scope + global SuperAdmin — full platform operator inventory.</summary>
	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	/// <summary>Tenant users may only target their URL-scoped face; returns NotFound to avoid leaking ids.</summary>
	private IActionResult? GateTenantFaceOrNotFound(int targetFaceId) =>
		_faceScope.GateTenantFaceOrNotFound(_access, User, targetFaceId);

	/// <summary>
	/// GET /api/faces
	/// Get list of all faces
	/// </summary>
	[HttpGet]
	public async Task<IActionResult> GetFaces([FromQuery] GetFacesQuery listQuery)
	{
		try
		{
			// Admin scope + SuperAdmin: full directory for CMS. Tenant scope: only the current face row.
			if (_faceScope.IsAdminFaceScope && !CanManageAllFaces())
				return Forbid();

			var page = listQuery.Page;
			var pageSize = listQuery.PageSize;

			IQueryable<Face> q = _context.Faces.AsNoTracking();
			if (!CanManageAllFaces())
				q = q.Where(f => f.Id == _faceScope.FaceId);

			if (!string.IsNullOrWhiteSpace(listQuery.Search))
			{
				var pattern = $"%{listQuery.Search.Trim()}%";
				q = q.Where(f =>
					EF.Functions.ILike(f.Index, pattern) ||
					EF.Functions.ILike(f.Title, pattern) ||
					(f.Description != null && EF.Functions.ILike(f.Description, pattern)));
			}

			if (!string.IsNullOrWhiteSpace(listQuery.Visibility) &&
				Enum.TryParse<FaceVisibility>(listQuery.Visibility, true, out var vis))
			{
				q = q.Where(f => f.Visibility == vis);
			}

			if (listQuery.IsPublic.HasValue)
				q = q.Where(f => f.IsPublic == listQuery.IsPublic.Value);

			// Paginated envelope replaces bare array (breaking change for admin; portal may still expect array on tenant routes).
			var totalCount = await q.CountAsync();
			var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
			page = clampedPage;

			var faces = await ListSortApplicators
				.ApplyFacesSort(q, listQuery.SortBy, listQuery.SortDir)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var items = faces.Select(FaceDto.From).ToList();

			_logger.LogInformation("Retrieved {Count} faces (page {Page})", items.Count, page);
			return Ok(ListPaginationHelper.BuildEnvelope(items, page, pageSize, totalCount, totalPages));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving faces");
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while retrieving faces" });
		}
	}

	/// <summary>
	/// GET /api/faces-config
	/// Get all faces with their pages configuration
	/// - For anonymous users on the public tenant: all public faces.
	/// - For authenticated users on the public tenant: public faces plus private faces they may enter
	///   (all faces for global Admin/SuperAdmin; otherwise faces with a <see cref="UserFaceRole"/> row).
	/// - For other tenants: the single face matching the URL scope.
	/// Used by frontend to generate routes before router initialization
	/// </summary>
	[HttpGet("config")]
	[AllowAnonymous]
	[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = false)]
	public async Task<IActionResult> GetFacesConfig(CancellationToken cancellationToken)
	{
		try
		{
			if (_faceScope.IsAdminFaceScope &&
				(User.Identity?.IsAuthenticated != true || !CanManageAllFaces()))
				return Forbid();

			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			var facesConfig = await _facesConfig.GetFacesConfigAsync(User, userId, cancellationToken);
			Response.Headers.CacheControl = User.Identity?.IsAuthenticated == true
				? "private, max-age=60"
				: "public, max-age=120";
			return Ok(facesConfig);
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
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
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while retrieving faces config" });
		}
	}

	/// <summary>
	/// GET /api/faces/face-roles
	/// Returns list of face-scoped roles (for role selector on first visit to a private face).
	/// </summary>
	[HttpGet("face-roles")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(IEnumerable<FaceRoleSummaryDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetFaceRoles()
	{
		try
		{
			var q = _context.UserRoles
				.AsNoTracking()
				.Where(r => r.Scope == RoleScope.Face);
			// G10: do not expose privileged face roles to anonymous / tenant users (A16).
			if (!CanManageAllFaces())
				q = q.Where(r =>
					r.Name == UserRole.FaceRoleNames.FaceUser ||
					r.Name == UserRole.FaceRoleNames.Inzerent ||
					r.Name == UserRole.FaceRoleNames.Subscriber ||
					r.Name == UserRole.FaceRoleNames.FaceHost);

			var roles = await q
				.OrderBy(r => r.Name)
				.Select(r => new FaceRoleSummaryDto { Id = r.Id, Name = r.Name })
				.ToListAsync();
			return Ok(roles);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving face roles");
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while retrieving face roles" });
		}
	}

	/// <summary>
	/// PUT /api/faces/{id}/my-role
	/// Set current user's face role for the given face. Used when user selects role on first visit to a private face.
	/// </summary>
	[HttpPut("{id}/my-role")]
	[ProducesResponseType(typeof(FaceRoleResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> SetMyFaceRole(int id, [FromBody] SetMyFaceRoleModel model)
	{
		if (model == null || model.UserRoleId <= 0)
		{
			return BadRequest(new ErrorResponseDto { Error = "userRoleId is required" });
		}

		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
		{
			return Unauthorized();
		}

		var scopeGate = GateTenantFaceOrNotFound(id);
		if (scopeGate != null)
			return scopeGate;

		try
		{
			var face = await _context.Faces.FindAsync(id);
			if (face == null)
			{
				return NotFound(new ErrorResponseDto { Error = "Face not found" });
			}

			var role = await _context.UserRoles.FindAsync(model.UserRoleId);
			if (role == null || role.Scope != RoleScope.Face)
			{
				return BadRequest(new ErrorResponseDto { Error = "Invalid face role" });
			}

			if (!CanManageAllFaces() && !FaceRoleSelfServiceRules.IsSelfAssignableFaceRoleName(role.Name))
			{
				_logger.LogWarning("User {UserId} attempted self-assign to non-whitelisted face role {RoleName}", userId, role.Name);
				return Forbid();
			}

			var existing = await _context.UserFaceRoles
				.FirstOrDefaultAsync(ufr => ufr.UserId == userId && ufr.FaceId == id);

			string? previousRoleName = null;
			if (existing != null)
			{
				previousRoleName = await _context.UserRoles.AsNoTracking()
					.Where(r => r.Id == existing.UserRoleId)
					.Select(r => r.Name)
					.FirstOrDefaultAsync();
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
				await UserFaceProfileEnsure.GetOrCreateAsync(
					_context,
					userProfile.Id,
					id,
					UserFaceProfileEnsure.Options.ForFaceRole(
						FaceRoleParticipation.IsActiveForFaceRoleName(role.Name),
						faceRoleIntroCompleted: true));
			}

			await _context.SaveChangesAsync();
			_logger.LogInformation("User {UserId} set face role to {RoleName} for face {FaceId}", userId, role.Name, id);
			SecurityAuditLog.FaceRoleChanged(_logger, userId, id, previousRoleName, role.Name, HttpContext.TraceIdentifier);
			return Ok(new FaceRoleResultDto { UserRoleId = model.UserRoleId, UserRoleName = role.Name });
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error setting face role for face {FaceId}", id);
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while setting face role" });
		}
	}

	/// <summary>
	/// POST /api/faces/{id}/visit — mark current user as having switched to this face (Visited = true).
	/// </summary>
	[HttpPost("{id:int}/visit")]
	[ProducesResponseType(typeof(VisitedResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> MarkFaceVisited(int id)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var visitGate = GateTenantFaceOrNotFound(id);
		if (visitGate != null)
			return visitGate;

		var face = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
		if (face == null)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);
		if (userProfile == null)
			return BadRequest(new ErrorResponseDto { Error = "User profile not found" });

		await UserFaceProfileEnsure.GetOrCreateAsync(
			_context,
			userProfile.Id,
			id,
			UserFaceProfileEnsure.Options.ForVisit);

		await _context.SaveChangesAsync();
		_facesConfig.InvalidateAll();
		return Ok(new VisitedResultDto());
	}

	/// <summary>
	/// POST /api/faces/{id}/exit-face — leave non-host participation; purge face-scoped social data and reset role to FACE_HOST.
	/// </summary>
	[HttpPost("{id:int}/exit-face")]
	[ProducesResponseType(typeof(ExitFaceResultDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ExitFace(int id)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var exitGate = GateTenantFaceOrNotFound(id);
		if (exitGate != null)
			return exitGate;

		var face = await _context.Faces.FirstOrDefaultAsync(f => f.Id == id);
		if (face == null)
			return NotFound(new ErrorResponseDto { Error = "Face not found" });

		if (FaceScopeConstants.IsAdminFaceIndex(face.Index))
			return BadRequest(new ErrorResponseDto { Error = "Cannot exit the admin scope face" });

		var hostRole = await _context.UserRoles
			.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost && r.Scope == RoleScope.Face);
		if (hostRole == null)
			return StatusCode(500, new ErrorResponseDto { Error = "FACE_HOST role missing" });

		var ufr = await _context.UserFaceRoles
			.Include(x => x.UserRole)
			.FirstOrDefaultAsync(x => x.UserId == userId && x.FaceId == id);
		if (ufr?.UserRole == null)
			return BadRequest(new ErrorResponseDto { Error = "No face role for this face" });
		if (FaceRoleParticipation.IsHostFaceRole(ufr.UserRole.Name))
			return BadRequest(new ErrorResponseDto { Error = "Already in host role" });

		var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);
		if (userProfile == null)
			return BadRequest(new ErrorResponseDto { Error = "User profile not found" });

		var myUfp = await _context.UserFaceProfiles
			.FirstOrDefaultAsync(x => x.UserProfileId == userProfile.Id && x.FaceId == id);
		if (myUfp == null)
			return BadRequest(new ErrorResponseDto { Error = "Face profile not found" });

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
		return Ok(new ExitFaceResultDto { Message = "Exited face", UserRoleId = hostRole.Id, UserRoleName = hostRole.Name });
	}

	/// <summary>
	/// GET /api/faces/{id}
	/// Get face by ID
	/// </summary>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(FaceDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetFace(int id)
	{
		try
		{
			var gate = GateTenantFaceOrNotFound(id);
			if (gate != null)
				return gate;

			var face = await _context.Faces.FindAsync(id);

			if (face == null)
			{
				_logger.LogWarning("Face not found: {FaceId}", id);
				return NotFound(new ErrorResponseDto { Error = "Face not found" });
			}

			var faceDto = FaceDto.From(face);

			_logger.LogInformation("Retrieved face: {FaceId}", id);
			return Ok(faceDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving face: {FaceId}", id);
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while retrieving face" });
		}
	}

	/// <summary>
	/// POST /api/faces
	/// Create a new face
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(FaceDto), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> CreateFace([FromBody] CreateFaceModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		if (!CanManageAllFaces())
			return Forbid();

		try
		{
			// Check if index already exists
			if (FaceScopeConstants.IsAdminFaceIndex(model.Index))
				return BadRequest(new ErrorResponseDto { Error = "The admin index is reserved for the platform scope face" });

			var existingFace = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Index == model.Index);
			if (existingFace != null)
			{
				_logger.LogWarning("Face with index already exists: {Index}", model.Index);
				return BadRequest(new ErrorResponseDto { Error = "Face with this index already exists" });
			}

			var gradient = string.IsNullOrWhiteSpace(model.GradientSettings)
				? FaceGradientPresets.JsonForFaceIndex(model.Index)
				: model.GradientSettings;

			var face = new Face
			{
				Index = model.Index,
				Title = model.Title,
				Description = model.Description,
				GradientSettings = gradient,
				IsPublic = model.IsPublic,
				Visibility = model.Visibility ?? FaceVisibility.Public,
				AllowRecensions = model.AllowRecensions ?? false,
				ChatRoomsCreate = model.ChatRoomsCreate ?? false,
				VideoLoungesCreate = model.VideoLoungesCreate ?? false,
				CreatedAt = DateTime.UtcNow,
			};

			_context.Faces.Add(face);
			await _context.SaveChangesAsync();
			InvalidateFacesRoutingCache();

			// Default pages: Home; Wall for non-public. Typed list/detail flows are FE routes (/list/:id, future /detail/...).
			var homePageType = await _context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "home");
			var wallPageType = await _context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "wall");

			var defaultPages = new List<Page>();
			var pageIndex = 0;
			if (homePageType != null)
			{
				defaultPages.Add(new Page { FaceId = face.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = pageIndex++, CreatedAt = DateTime.UtcNow });
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

			await _profileDetailTemplates.EnsureForFaceAsync(face.Id);

			var faceDto = FaceDto.From(face);

			return CreatedAtAction(nameof(GetFace), new { id = face.Id }, faceDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating face");
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while creating face" });
		}
	}

	/// <summary>
	/// PUT /api/faces/{id}
	/// Update face by ID
	/// </summary>
	[HttpPut("{id}")]
	[ProducesResponseType(typeof(FaceDto), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> UpdateFace(int id, [FromBody] UpdateFaceModel model)
	{
		if (!ModelState.IsValid)
		{
			return BadRequest(ModelState);
		}

		if (!CanManageAllFaces())
			return Forbid();

		try
		{
			var face = await _context.Faces.FindAsync(id);

			if (face == null)
			{
				_logger.LogWarning("Face not found for update: {FaceId}", id);
				return NotFound(new ErrorResponseDto { Error = "Face not found" });
			}

			if (FaceScopeConstants.IsAdminFaceIndex(face.Index))
				return BadRequest(new ErrorResponseDto { Error = "The admin scope face cannot be modified" });

			if (model.Index != null && FaceScopeConstants.IsAdminFaceIndex(model.Index))
				return BadRequest(new ErrorResponseDto { Error = "The admin index is reserved for the platform scope face" });

			// Check if index already exists (excluding current face)
			if (model.Index != null && model.Index != face.Index)
			{
				var existingFace = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Index == model.Index && f.Id != id);
				if (existingFace != null)
				{
					_logger.LogWarning("Face with index already exists: {Index}", model.Index);
					return BadRequest(new ErrorResponseDto { Error = "Face with this index already exists" });
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
			if (model.ChatRoomsCreate.HasValue)
			{
				face.ChatRoomsCreate = model.ChatRoomsCreate.Value;
			}

			if (model.VideoLoungesCreate.HasValue)
			{
				face.VideoLoungesCreate = model.VideoLoungesCreate.Value;
			}

			face.UpdatedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();
			InvalidateFacesRoutingCache();

			var faceDto = FaceDto.From(face);

			_logger.LogInformation("Face updated: {FaceId}", id);
			return Ok(faceDto);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating face: {FaceId}", id);
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while updating face" });
		}
	}

	/// <summary>
	/// DELETE /api/faces/{id}
	/// Delete face by ID
	/// </summary>
	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
	public async Task<IActionResult> DeleteFace(int id)
	{
		if (!CanManageAllFaces())
			return Forbid();

		try
		{
			var face = await _context.Faces.FindAsync(id);

			if (face == null)
			{
				_logger.LogWarning("Face not found for deletion: {FaceId}", id);
				return NotFound(new ErrorResponseDto { Error = "Face not found" });
			}

			if (FaceScopeConstants.IsAdminFaceIndex(face.Index))
				return BadRequest(new ErrorResponseDto { Error = "The admin scope face cannot be deleted" });

			var profilesWithFace = await _context.UserProfiles
				.Where(p => p.LastSelectedFaceId == id)
				.ToListAsync();
			foreach (var p in profilesWithFace)
			{
				p.LastSelectedFaceId = null;
				p.UpdatedAt = DateTime.UtcNow;
			}

			_context.Faces.Remove(face);
			await _context.SaveChangesAsync();
			InvalidateFacesRoutingCache();

			_logger.LogInformation("Face deleted: {FaceId}", id);
			return NoContent();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting face: {FaceId}", id);
			return StatusCode(500, new ErrorResponseDto { Error = "An error occurred while deleting face" });
		}
	}
}
