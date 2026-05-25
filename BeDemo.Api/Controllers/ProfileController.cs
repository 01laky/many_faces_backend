using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Models;
using BeDemo.Api.Data;
using BeDemo.Api.Models.Requests.Profile;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using BeDemo.Api.Validation.Files;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ApplicationDbContext _context;
	private readonly IWebHostEnvironment _env;
	private readonly ILogger<ProfileController> _logger;
	private readonly IFileValidator _fileValidator;
	private readonly IUploadSignedUrlService _uploadUrls;

	private static readonly string[] AvatarDirSegments = ["uploads", "avatars"];
	private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

	public ProfileController(
		UserManager<ApplicationUser> userManager,
		ApplicationDbContext context,
		IWebHostEnvironment env,
		ILogger<ProfileController> logger,
		IFileValidator fileValidator,
		IUploadSignedUrlService uploadUrls)
	{
		_userManager = userManager;
		_context = context;
		_env = env;
		_logger = logger;
		_fileValidator = fileValidator;
		_uploadUrls = uploadUrls;
	}

	/// <summary>
	/// GET /api/profile/me
	/// Returns current user profile. Optional ?faceId= for resolved avatar (local for that face, else global).
	/// </summary>
	[HttpGet("me")]
	public async Task<IActionResult> GetMyProfile([FromQuery] ProfileMeQuery query)
	{
		var userId = _userManager.GetUserId(User);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var faceId = query.FaceId;
		var user = await _userManager.FindByIdAsync(userId);
		if (user == null)
			return Unauthorized();

		var profile = await _context.UserProfiles
			.Include(p => p.UserFaceProfiles)
			.FirstOrDefaultAsync(p => p.UserId == userId);

		if (profile == null)
		{
			profile = new UserProfile { UserId = userId };
			_context.UserProfiles.Add(profile);
			await _context.SaveChangesAsync();
		}

		string? faceAvatarUrl = null;
		if (faceId.HasValue)
		{
			var faceProfile = await _context.UserFaceProfiles
				.FirstOrDefaultAsync(ufp => ufp.UserProfileId == profile.Id && ufp.FaceId == faceId.Value);
			faceAvatarUrl = faceProfile?.AvatarUrl;
		}

		return Ok(new
		{
			firstName = user.FirstName,
			lastName = user.LastName,
			email = user.Email,
			enableAnimatedGradient = profile.EnableAnimatedGradient,
			preferredUiLanguage = profile.PreferredUiLanguage,
			lastSelectedFaceId = profile.LastSelectedFaceId,
			globalAvatarUrl = _uploadUrls.ToAbsoluteSignedUrl(profile.AvatarUrl, Request.Scheme, Request.Host.Value!),
			faceAvatarUrl = _uploadUrls.ToAbsoluteSignedUrl(faceAvatarUrl, Request.Scheme, Request.Host.Value!),
		});
	}

	/// <summary>
	/// PUT /api/profile/me - update name
	/// </summary>
	[HttpPut("me")]
	public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest model)
	{
		var userId = _userManager.GetUserId(User);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var user = await _userManager.FindByIdAsync(userId);
		if (user == null)
			return Unauthorized();

		var nameChanged = false;
		if (model.FirstName != null)
		{
			user.FirstName = model.FirstName.Trim().Length > 0 ? model.FirstName.Trim() : null;
			nameChanged = true;
		}
		if (model.LastName != null)
		{
			user.LastName = model.LastName.Trim().Length > 0 ? model.LastName.Trim() : null;
			nameChanged = true;
		}

		if (nameChanged)
			await _userManager.UpdateAsync(user);

		var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
		if (profile == null)
		{
			profile = new UserProfile { UserId = userId };
			_context.UserProfiles.Add(profile);
		}

		var profileDirty = false;

		if (model.EnableAnimatedGradient.HasValue)
		{
			profile.EnableAnimatedGradient = model.EnableAnimatedGradient.Value;
			profileDirty = true;
		}

		if (model.ClearPreferredUiLanguage)
		{
			profile.PreferredUiLanguage = null;
			profileDirty = true;
		}
		else if (model.PreferredUiLanguage != null)
		{
			var lang = model.PreferredUiLanguage.Trim();
			if (lang.Length == 0)
			{
				profile.PreferredUiLanguage = null;
				profileDirty = true;
			}
			else if (!PortalSupportedUiLanguages.IsAllowed(lang))
			{
				return BadRequest(new { error = "Unsupported UI language" });
			}
			else
			{
				profile.PreferredUiLanguage = lang.ToLowerInvariant();
				profileDirty = true;
			}
		}

		if (model.ClearLastSelectedFaceId)
		{
			profile.LastSelectedFaceId = null;
			profileDirty = true;
		}
		else if (model.LastSelectedFaceId.HasValue)
		{
			var faceAccess = await ValidateUserCanAccessFaceAsync(userId, profile.Id, model.LastSelectedFaceId.Value);
			if (faceAccess != null)
				return faceAccess;
			profile.LastSelectedFaceId = model.LastSelectedFaceId.Value;
			profileDirty = true;
		}

		if (profileDirty)
		{
			profile.UpdatedAt = DateTime.UtcNow;
			await _context.SaveChangesAsync();
		}

		return Ok(new { message = "Profile updated" });
	}

	/// <summary>
	/// GET /api/profile/me/faces/{faceId}/settings — grid component UI prefs for the current user on a face.
	/// </summary>
	[HttpGet("me/faces/{faceId:int}/settings")]
	public async Task<IActionResult> GetMyFaceGridSettings(int faceId)
	{
		var userId = _userManager.GetUserId(User);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var profile = await EnsureUserProfileAsync(userId);
		var gate = await ValidateUserCanAccessFaceAsync(userId, profile.Id, faceId);
		if (gate != null)
			return gate;

		var ufp = await _context.UserFaceProfiles
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.UserProfileId == profile.Id && x.FaceId == faceId);

		if (!ProfileGridSettingsJson.TryParseResponse(ufp?.Settings, out var gridComponents, out var parseError))
			return BadRequest(new { error = parseError });

		return Ok(new { gridComponents });
	}

	/// <summary>
	/// PUT /api/profile/me/faces/{faceId}/settings — merge grid component UI prefs (e.g. carousel autoplay).
	/// </summary>
	[HttpPut("me/faces/{faceId:int}/settings")]
	public async Task<IActionResult> UpdateMyFaceGridSettings(int faceId, [FromBody] UpdateFaceGridSettingsRequest model)
	{
		var userId = _userManager.GetUserId(User);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var profile = await EnsureUserProfileAsync(userId);
		var gate = await ValidateUserCanAccessFaceAsync(userId, profile.Id, faceId);
		if (gate != null)
			return gate;

		var patch = new JsonObject();
		if (model.GridComponents != null)
		{
			foreach (var (componentId, entry) in model.GridComponents)
			{
				if (string.IsNullOrWhiteSpace(componentId))
					continue;
				var node = new JsonObject();
				if (entry.Autoplay.HasValue)
					node["autoplay"] = entry.Autoplay.Value;
				patch[componentId] = node;
			}
		}

		var ufp = await UserFaceProfileEnsure.GetOrCreateAsync(
			_context,
			profile.Id,
			faceId,
			UserFaceProfileEnsure.Options.Passive);

		if (!ProfileGridSettingsJson.TryMergePatch(ufp.Settings, patch, out var merged, out var mergeError))
			return BadRequest(new { error = mergeError });

		ufp.Settings = merged;
		ufp.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		if (!ProfileGridSettingsJson.TryParseResponse(merged, out var gridComponents, out _))
			gridComponents = new JsonObject();

		return Ok(new { gridComponents });
	}

	private async Task<UserProfile> EnsureUserProfileAsync(string userId)
	{
		var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
		if (profile != null)
			return profile;

		profile = new UserProfile { UserId = userId };
		_context.UserProfiles.Add(profile);
		await _context.SaveChangesAsync();
		return profile;
	}

	/// <summary>User may persist prefs for public faces or faces they participate in.</summary>
	private async Task<IActionResult?> ValidateUserCanAccessFaceAsync(string userId, int userProfileId, int faceId)
	{
		var face = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faceId);
		if (face == null)
			return NotFound(new { error = "Face not found" });

		if (face.IsPublic)
			return null;

		var hasRole = await _context.UserFaceRoles.AnyAsync(ufr => ufr.UserId == userId && ufr.FaceId == faceId);
		if (hasRole)
			return null;

		var hasProfile = await _context.UserFaceProfiles.AnyAsync(ufp =>
			ufp.UserProfileId == userProfileId && ufp.FaceId == faceId);
		if (hasProfile)
			return null;

		return StatusCode(StatusCodes.Status403Forbidden, new { error = "Face not accessible" });
	}

	/// <summary>
	/// POST /api/profile/me/avatar - upload global avatar
	/// </summary>
	[HttpPost("me/avatar")]
	[EnableRateLimiting("upload-write")]
	public async Task<IActionResult> UploadMyAvatar([FromForm] AvatarUploadRequest request)
	{
		var userId = _userManager.GetUserId(User);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var file = request.File!;
		var (path, error) = await SaveAvatarFile(file, userId, null);
		if (error != null)
			return BadRequest(new { error });

		var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
		if (profile == null)
		{
			profile = new UserProfile { UserId = userId };
			_context.UserProfiles.Add(profile);
			await _context.SaveChangesAsync();
		}

		profile.AvatarUrl = path;
		profile.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync();

		return Ok(new
		{
			avatarUrl = _uploadUrls.ToAbsoluteSignedUrl(path, Request.Scheme, Request.Host.Value!),
		});
	}

	/// <summary>
	/// POST /api/profile/me/faces/{faceId}/avatar - upload face-specific avatar
	/// </summary>
	[HttpPost("me/faces/{faceId:int}/avatar")]
	[EnableRateLimiting("upload-write")]
	public async Task<IActionResult> UploadMyFaceAvatar(int faceId, [FromForm] AvatarUploadRequest request)
	{
		var userId = _userManager.GetUserId(User);
		if (string.IsNullOrEmpty(userId))
			return Unauthorized();

		var file = request.File!;
		var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
		if (profile == null)
		{
			profile = new UserProfile { UserId = userId };
			_context.UserProfiles.Add(profile);
			await _context.SaveChangesAsync();
		}

		var faceExists = await _context.Faces.AnyAsync(f => f.Id == faceId);
		if (!faceExists)
			return NotFound(new { error = "Face not found" });

		var (path, error) = await SaveAvatarFile(file, userId, faceId);
		if (error != null)
			return BadRequest(new { error });

		var faceProfile = await UserFaceProfileEnsure.GetOrCreateAsync(
			_context,
			profile.Id,
			faceId,
			UserFaceProfileEnsure.Options.Passive);
		faceProfile.AvatarUrl = path;
		faceProfile.UpdatedAt = DateTime.UtcNow;

		await _context.SaveChangesAsync();

		return Ok(new
		{
			avatarUrl = _uploadUrls.ToAbsoluteSignedUrl(path, Request.Scheme, Request.Host.Value!),
		});
	}

	/// <summary>
	/// Persists avatar bytes under <c>wwwroot/uploads/avatars/{userId}/</c> with SHV2 path containment (BE-U4) and size cap (BE-U2).
	/// </summary>
	private async Task<(string? relativePath, string? error)> SaveAvatarFile(IFormFile file, string userId, int? faceId)
	{
		if (!UploadPathSecurity.IsSafePathSegment(userId))
			return (null, "Invalid upload path");

		var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
		if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
			return (null, "Invalid file type. Allowed: " + string.Join(", ", AllowedExtensions));

		if (file.Length > UploadLimits.AvatarMaxBytes)
			return (null, UploadLimits.FormatMaxFileSizeMessage(UploadLimits.AvatarMaxBytes));

		await using (var peek = file.OpenReadStream())
		{
			var (ok, errorCode) = await _fileValidator.ValidateImageAsync(peek, file.FileName);
			if (!ok)
				return (null, errorCode ?? "val_file_format");
		}

		var webRoot = _env.WebRootPath;
		if (string.IsNullOrEmpty(webRoot))
			webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");

		var fileName = faceId.HasValue ? $"face_{faceId.Value}{ext}" : $"global{ext}";
		if (!UploadPathSecurity.TryResolveFileUnderWebRoot(
				webRoot,
				[.. AvatarDirSegments, userId],
				fileName,
				out var fullPath,
				out var pathError))
			return (null, pathError);

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
			await using (var stream = new FileStream(fullPath, FileMode.Create))
				await file.CopyToAsync(stream);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to save avatar file under uploads (user folder redacted)");
			return (null, "Server error saving file");
		}

		var relativePath = UploadPathSecurity.BuildUploadUrlPath(
			AvatarDirSegments[0],
			AvatarDirSegments[1],
			userId,
			fileName);
		return (relativePath, null);
	}
}
