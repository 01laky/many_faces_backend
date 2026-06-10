using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Authenticated creator API: returns a unified list of the caller's albums, blogs, and reels with safe moderation fields.
/// Internal AI diagnostics (raw model reason, trace ids) are never exposed here—only creator-safe labels and messages.
/// </summary>
[ApiController]
[Route("api/my/content-submissions")]
[Authorize]
public sealed class MyContentSubmissionsController : ApiControllerBase
{
	private readonly ApplicationDbContext _context;

	public MyContentSubmissionsController(ApplicationDbContext context)
	{
		_context = context;
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyList<MyContentSubmissionDto>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetMine()
	{
		if (string.IsNullOrWhiteSpace(UserId))
			return Unauthorized();

		var items = new List<MyContentSubmissionDto>();

		// Project each entity type into the same DTO shape so the FE can render a single "My submissions" grid.
		var albums = await _context.Albums
			.Include(a => a.AlbumFaces).ThenInclude(af => af.Face)
			.Where(a => a.CreatorId == UserId)
			.Select(a => new MyContentSubmissionDto(
				ModeratedContentType.Album,
				a.Id,
				a.Title,
				a.AlbumFaces.Select(af => af.FaceId).FirstOrDefault(),
				a.AlbumFaces.Select(af => af.Face.Title).FirstOrDefault() ?? string.Empty,
				a.ApprovalStatus,
				a.AiReviewStatus,
				ContentModerationHelpers.CreatorStatusLabel(a.ApprovalStatus, a.AiReviewStatus),
				a.AiReviewUserMessage,
				a.HumanDecisionReason,
				a.SubmittedAtUtc,
				a.UpdatedAt,
				a.CreatedAt,
				ContentModerationHelpers.IsCreatorEditable(a.ApprovalStatus),
				ContentModerationHelpers.IsCreatorDeletable(a.ApprovalStatus)))
			.ToListAsync();
		items.AddRange(albums);

		var blogs = await _context.Blogs
			.Include(b => b.Face)
			.Where(b => b.CreatorId == UserId)
			.Select(b => new MyContentSubmissionDto(
				ModeratedContentType.Blog,
				b.Id,
				b.Title,
				b.FaceId,
				b.Face.Title,
				b.ApprovalStatus,
				b.AiReviewStatus,
				ContentModerationHelpers.CreatorStatusLabel(b.ApprovalStatus, b.AiReviewStatus),
				b.AiReviewUserMessage,
				b.HumanDecisionReason,
				b.SubmittedAtUtc,
				b.UpdatedAt,
				b.CreatedAt,
				ContentModerationHelpers.IsCreatorEditable(b.ApprovalStatus),
				ContentModerationHelpers.IsCreatorDeletable(b.ApprovalStatus)))
			.ToListAsync();
		items.AddRange(blogs);

		var reels = await _context.Reels
			.Include(r => r.ReelFaces).ThenInclude(rf => rf.Face)
			.Where(r => r.CreatorId == UserId)
			.Select(r => new MyContentSubmissionDto(
				ModeratedContentType.Reel,
				r.Id,
				r.Title,
				r.ReelFaces.Select(rf => rf.FaceId).FirstOrDefault(),
				r.ReelFaces.Select(rf => rf.Face.Title).FirstOrDefault() ?? string.Empty,
				r.ApprovalStatus,
				r.AiReviewStatus,
				ContentModerationHelpers.CreatorStatusLabel(r.ApprovalStatus, r.AiReviewStatus),
				r.AiReviewUserMessage,
				r.HumanDecisionReason,
				r.SubmittedAtUtc,
				r.UpdatedAt,
				r.CreatedAt,
				ContentModerationHelpers.IsCreatorEditable(r.ApprovalStatus),
				ContentModerationHelpers.IsCreatorDeletable(r.ApprovalStatus)))
			.ToListAsync();
		items.AddRange(reels);

		return Ok(items.OrderByDescending(i => i.SubmittedAtUtc ?? i.CreatedAt));
	}
}

/// <summary>Single row in the creator submissions feed (camelCase JSON from ASP.NET defaults).</summary>
public sealed record MyContentSubmissionDto(
	ModeratedContentType ContentType,
	int ContentId,
	string Title,
	int FaceId,
	string FaceTitle,
	ContentApprovalStatus ApprovalStatus,
	AiReviewStatus AiReviewStatus,
	string CreatorStatusLabel,
	string? AiReviewUserMessage,
	string? HumanDecisionReason,
	DateTime? SubmittedAtUtc,
	DateTime? UpdatedAt,
	DateTime CreatedAt,
	bool CanEdit,
	bool CanDelete);
