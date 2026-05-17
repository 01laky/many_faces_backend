using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        if (model.FirstName != null)
            user.FirstName = model.FirstName.Trim().Length > 0 ? model.FirstName.Trim() : null;
        if (model.LastName != null)
            user.LastName = model.LastName.Trim().Length > 0 ? model.LastName.Trim() : null;

        await _userManager.UpdateAsync(user);
        return Ok(new { message = "Profile updated" });
    }

    /// <summary>
    /// POST /api/profile/me/avatar - upload global avatar
    /// </summary>
    [HttpPost("me/avatar")]
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

        var faceProfile = await _context.UserFaceProfiles
            .FirstOrDefaultAsync(ufp => ufp.UserProfileId == profile.Id && ufp.FaceId == faceId);

        if (faceProfile == null)
        {
            faceProfile = new UserFaceProfile
            {
                UserProfileId = profile.Id,
                FaceId = faceId,
                AvatarUrl = path,
            };
            _context.UserFaceProfiles.Add(faceProfile);
        }
        else
        {
            faceProfile.AvatarUrl = path;
            faceProfile.UpdatedAt = DateTime.UtcNow;
        }

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
                [..AvatarDirSegments, userId],
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
