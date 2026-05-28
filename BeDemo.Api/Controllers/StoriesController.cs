using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.Stories;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Grid;
using BeDemo.Api.Utils;
using BeDemo.Api.Validation.Files;

namespace BeDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StoriesController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IStoryLifecycleService _lifecycle;
	private readonly IWebHostEnvironment _env;
	private readonly ILogger<StoriesController> _logger;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IFileValidator _fileValidator;
	private readonly IUploadSignedUrlService _uploadUrls;
	private readonly IStoryGridListService _storyGridList;

	public StoriesController(
		ApplicationDbContext context,
		IStoryLifecycleService lifecycle,
		IWebHostEnvironment env,
		ILogger<StoriesController> logger,
		IFaceScopeContext faceScope,
		IAccessEvaluator access,
		IFileValidator fileValidator,
		IUploadSignedUrlService uploadUrls,
		IStoryGridListService storyGridList)
	{
		_context = context;
		_lifecycle = lifecycle;
		_env = env;
		_logger = logger;
		_faceScope = faceScope;
		_access = access;
		_fileValidator = fileValidator;
		_uploadUrls = uploadUrls;
		_storyGridList = storyGridList;
	}

	private string? UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

	/// <summary>Maps stored <c>/uploads/...</c> DB paths to HMAC-signed serve URLs for clients (BE-U3).</summary>
	private string? SignStoredImageUrl(string? storedPath) =>
		_uploadUrls.ToAbsoluteSignedUrl(storedPath, Request.Scheme, Request.Host.Value!);

	private bool CanManageAllFaces() => _access.CanManageAllFaces(User);

	/// <summary>
	/// Tenants may only mutate stories that are untargeted (draft) or targeted at their face.
	/// Global admins under /admin/ may touch any story.
	/// </summary>
	private IActionResult? GateStoryForScope(Story story)
	{
		if (CanManageAllFaces())
			return null;
		if (!story.StoryFaces.Any())
			return null;
		if (story.StoryFaces.All(sf => sf.FaceId != _faceScope.FaceId))
			return NotFound(new { error = "Story not found" });
		return null;
	}

	/// <summary>Stories for face — paginated envelope; operator sees unpublished/draft (§1.1).</summary>
	[HttpGet]
	public async Task<IActionResult> ListForFace([FromQuery] StoryListQuery query, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var result = await _storyGridList.GetStoriesAsync(User, UserId, query, cancellationToken);
		return Ok(result);
	}

	/// <summary>Current user's stories (all states), optional face targeting filter.</summary>
	[HttpGet("me")]
	public async Task<IActionResult> ListMine([FromQuery] StoryMineQuery listQuery, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var query = _context.Stories
			.AsNoTracking()
			.Include(s => s.StoryFaces)
			.Include(s => s.Images)
			.Where(s => s.CreatorId == UserId);

		var filterFace = _faceScope.ResolveDataFaceId(listQuery.FaceId);
		if (listQuery.FaceId.HasValue || !_faceScope.IsAdminFaceScope)
		{
			query = query.Where(s =>
				!s.StoryFaces.Any() ||
				s.StoryFaces.Any(sf => sf.FaceId == filterFace));
		}

		var list = await query
			.OrderByDescending(s => s.CreatedAt)
			.Select(s => new
			{
				s.Id,
				s.Title,
				s.State,
				s.PublishedAt,
				s.ExpiresAt,
				s.ScheduledPublishAt,
				s.CreatedAt,
				imageCount = s.Images.Count,
				faceIds = s.StoryFaces.Select(sf => sf.FaceId).ToList(),
			})
			.ToListAsync(cancellationToken);

		return Ok(list);
	}

	[HttpGet("{id:int}")]
	public async Task<IActionResult> GetDetail(int id, [FromQuery] StoryDetailQuery detailQuery, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var effectiveFaceId = _faceScope.ResolveDataFaceId(detailQuery.FaceId);

		var story = await _context.Stories
			.Include(s => s.Creator)
			.Include(s => s.StoryFaces)
			.ThenInclude(sf => sf.Face)
			.Include(s => s.Images)
			.Include(s => s.Likes)
			.Include(s => s.Comments)
			.Include(s => s.Views)
			.ThenInclude(v => v.Viewer)
			.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

		if (story == null)
			return NotFound(new { error = "Story not found" });

		var operatorInventory = CanManageAllFaces();

		if (!StoryVisibility.IsTargetedForFace(story, effectiveFaceId))
			return NotFound(new { error = "Story not found" });

		var isCreator = story.CreatorId == UserId;
		var now = DateTime.UtcNow;
		var isLive = story.State == StoryState.Published &&
					 story.PublishedAt.HasValue && story.PublishedAt <= now &&
					 story.ExpiresAt.HasValue && story.ExpiresAt > now;

		// Portal viewers: membership + live window only. Operators: full inventory (§1.1), same as ListForFace.
		if (!operatorInventory && !isCreator)
		{
			if (!await StoryViewerRules.ViewerHasFaceMembershipAsync(_context, UserId, effectiveFaceId, cancellationToken))
				return NotFound(new { error = "Story not found" });

			if (!isLive)
				return NotFound(new { error = "Story not found" });
		}

		var images = story.Images.OrderBy(i => i.SortOrder).Select(i => new
		{
			i.Id,
			imageUrl = SignStoredImageUrl(i.ImageUrl),
			i.Description,
			i.SortOrder,
		}).ToList();

		var creatorName = ((story.Creator.FirstName ?? "") + " " + (story.Creator.LastName ?? "")).Trim();
		var faces = story.StoryFaces
			.Select(sf => new { faceId = sf.FaceId, title = sf.Face.Title })
			.ToList();

		const int maxViewers = 100;
		var viewersPayload = (isCreator || operatorInventory)
			? story.Views
				.OrderByDescending(v => v.ViewedAt)
				.Take(maxViewers)
				.Select(v => new
				{
					v.ViewerUserId,
					viewerName = ((v.Viewer.FirstName ?? "") + " " + (v.Viewer.LastName ?? "")).Trim(),
					v.ViewedAt,
				})
				.ToList()
			: null;

		return Ok(new
		{
			story.Id,
			story.Title,
			story.State,
			story.CreatorId,
			creatorName,
			faces,
			images,
			likesCount = story.Likes.Count,
			commentsCount = story.Comments.Count,
			isLikedByMe = story.Likes.Any(l => l.UserId == UserId),
			story.PublishedAt,
			story.ExpiresAt,
			story.ScheduledPublishAt,
			story.CreatedAt,
			story.UpdatedAt,
			viewCount = story.Views.Count,
			viewers = viewersPayload,
		});
	}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateStoryDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var faceTargets = dto.FaceIds;
		if (faceTargets == null || faceTargets.Count == 0)
			faceTargets = new List<int> { _faceScope.FaceId };
		else if (!CanManageAllFaces())
		{
			if (faceTargets.Any(fid => fid != _faceScope.FaceId))
				return BadRequest(new { error = "Stories may only target the current face scope" });
		}

		await _lifecycle.EnsureRoomForNewStoryAsync(UserId, cancellationToken);

		var story = new Story
		{
			CreatorId = UserId,
			Title = dto.Title.Trim(),
			State = StoryState.Draft,
		};

		_context.Stories.Add(story);
		await _context.SaveChangesAsync(cancellationToken);

		var valid = await _context.Faces
			.Where(f => faceTargets.Contains(f.Id))
			.Select(f => f.Id)
			.ToListAsync(cancellationToken);
		foreach (var fid in valid)
			_context.StoryFaces.Add(new StoryFace { StoryId = story.Id, FaceId = fid });
		if (valid.Count > 0)
			await _context.SaveChangesAsync(cancellationToken);

		return StatusCode(StatusCodes.Status201Created, new { story.Id });
	}

	[HttpPost("{id:int}/publish")]
	public async Task<IActionResult> Publish(int id, [FromBody] PublishStoryDto dto, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var storyPre = await _context.Stories.Include(s => s.StoryFaces).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
		if (storyPre == null)
			return NotFound(new { error = "Story not found" });
		var pubGate = GateStoryForScope(storyPre);
		if (pubGate != null)
			return pubGate;

		var (ok, err) = await _lifecycle.TryPublishAsync(UserId, id, dto.ScheduledPublishAt, cancellationToken);
		if (!ok)
		{
			return err switch
			{
				"not_found" => NotFound(new { error = "Story not found" }),
				"forbidden" => Forbid(),
				"invalid_images" => BadRequest(new { error = "Story must have between 1 and 10 images before publish" }),
				"already_published" => BadRequest(new { error = "Story is still live" }),
				_ => BadRequest(new { error = err }),
			};
		}

		return Ok(new { published = true });
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var story = await _context.Stories.Include(s => s.StoryFaces).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
		if (story == null)
			return NotFound(new { error = "Story not found" });
		if (story.CreatorId != UserId)
			return Forbid();
		var delGate = GateStoryForScope(story);
		if (delGate != null)
			return delGate;

		_context.Stories.Remove(story);
		await _context.SaveChangesAsync(cancellationToken);
		_logger.LogInformation("User {UserId} deleted story {StoryId}", UserId, id);
		return NoContent();
	}

	/// <summary>Record a view (idempotent per viewer).</summary>
	[HttpPost("{id:int}/view")]
	public async Task<IActionResult> RecordView(int id, [FromQuery] StoryViewQuery viewQuery, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var effectiveFaceId = _faceScope.ResolveDataFaceId(viewQuery.FaceId);

		if (!await StoryViewerRules.ViewerIsActiveNonHostInFaceAsync(_context, UserId, effectiveFaceId, cancellationToken))
			return Forbid();

		var story = await _context.Stories
			.Include(s => s.StoryFaces)
			.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

		if (story == null)
			return NotFound(new { error = "Story not found" });

		var now = DateTime.UtcNow;
		if (story.State != StoryState.Published || story.PublishedAt > now || story.ExpiresAt <= now)
			return NotFound(new { error = "Story not found" });

		if (!StoryVisibility.IsTargetedForFace(story, effectiveFaceId))
			return NotFound(new { error = "Story not found" });

		var existing = await _context.StoryViews.FirstOrDefaultAsync(
			v => v.StoryId == id && v.ViewerUserId == UserId,
			cancellationToken);
		if (existing != null)
			return Ok(new { recorded = false });

		_context.StoryViews.Add(new StoryView
		{
			StoryId = id,
			ViewerUserId = UserId,
			ViewedAt = now,
		});
		await _context.SaveChangesAsync(cancellationToken);
		return Ok(new { recorded = true });
	}

	[HttpPost("{id:int}/images")]
	[RequestSizeLimit(52_428_800)]
	public async Task<IActionResult> UploadImage(
		int id,
		[FromForm] StoryImageUploadForm form,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(UserId))
			return Unauthorized();

		var story = await _context.Stories.Include(s => s.Images).Include(s => s.StoryFaces).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
		if (story == null)
			return NotFound(new { error = "Story not found" });
		if (story.CreatorId != UserId)
			return Forbid();

		var imgGate = GateStoryForScope(story);
		if (imgGate != null)
			return imgGate;

		if (story.State == StoryState.Published)
		{
			var now = DateTime.UtcNow;
			if (story.ExpiresAt > now)
				return BadRequest(new { error = "Cannot add images to a live story" });
		}

		var file = form.File;
		var sortOrder = form.SortOrder;

		if (story.Images.Count >= 10)
			return BadRequest(new { error = "Maximum 10 images per story" });

		if (story.Images.Any(i => i.SortOrder == sortOrder))
			return BadRequest(new { error = "sortOrder already used" });

		await using (var peek = file.OpenReadStream())
		{
			var (ok, errorCode) = await _fileValidator.ValidateImageAsync(peek, file.FileName, cancellationToken);
			if (!ok)
				return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
				{
					["file"] = [errorCode ?? "val_file_format"],
				}));
		}

		// Persist under wwwroot/uploads/stories/{storyId}/ with path containment (SHV2 BE-U4).
		var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
			? Path.Combine(_env.ContentRootPath, "wwwroot")
			: _env.WebRootPath;

		var storyIdSegment = id.ToString();
		var ext = Path.GetExtension(file.FileName);
		if (string.IsNullOrEmpty(ext) || ext.Length > 10)
			ext = ".bin";
		// BE-U5: non-guessable file names (GUID) — do not derive from user-supplied base name.
		var fileName = $"{Guid.NewGuid():N}{ext}";
		if (!UploadPathSecurity.TryResolveFileUnderWebRoot(
				webRoot,
				["uploads", "stories", storyIdSegment],
				fileName,
				out var fullPath,
				out var pathError))
			return BadRequest(new { error = pathError ?? "Invalid upload path" });

		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
		await using (var stream = System.IO.File.Create(fullPath))
		{
			await file.CopyToAsync(stream, cancellationToken);
		}

		var url = UploadPathSecurity.BuildUploadUrlPath("uploads", "stories", storyIdSegment, fileName);
		var img = new StoryImage
		{
			StoryId = id,
			ImageUrl = url,
			Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
			SortOrder = sortOrder,
		};
		_context.StoryImages.Add(img);
		story.UpdatedAt = DateTime.UtcNow;
		await _context.SaveChangesAsync(cancellationToken);

		return Ok(new { img.Id, imageUrl = SignStoredImageUrl(url), img.SortOrder });
	}
}
