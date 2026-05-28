using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Reels;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Grid;

public sealed class ReelGridListService : IReelGridListService
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IOptions<PerformanceOptions> _perfOptions;

	public ReelGridListService(
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IAccessEvaluator access,
		IOptions<PerformanceOptions> perfOptions)
	{
		_context = context;
		_faceScope = faceScope;
		_access = access;
		_perfOptions = perfOptions;
	}

	public async Task<object> GetReelsAsync(
		ClaimsPrincipal user,
		string? userId,
		ReelListQuery listQuery,
		CancellationToken cancellationToken = default)
	{
		var operatorInventory = _access.CanManageAllFaces(user);
		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;
		var effectiveFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);

		IQueryable<Reel> query = _context.Reels.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.GridSnapshot);
		query = OperatorContentListFilters.ApplyReelPortalVisibility(query, operatorInventory);

		if (!string.IsNullOrWhiteSpace(listQuery.CreatorId))
		{
			var creatorId = listQuery.CreatorId.Trim();
			query = query.Where(r => r.CreatorId == creatorId);
			if (listQuery.FaceId is > 0)
			{
				var scopedFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
				query = query.Where(r =>
					!r.ReelFaces.Any() ||
					r.ReelFaces.Any(rf => rf.FaceId == scopedFaceId));
			}
		}
		else
		{
			query = query.Where(r =>
				!r.ReelFaces.Any() ||
				r.ReelFaces.Any(rf => rf.FaceId == effectiveFaceId));
		}

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var pattern = $"%{listQuery.Search.Trim()}%";
			query = query.Where(r =>
				EF.Functions.ILike(r.Title, pattern) ||
				(r.Description != null && EF.Functions.ILike(r.Description, pattern)));
		}

		if (!string.IsNullOrWhiteSpace(listQuery.ApprovalStatus) &&
			Enum.TryParse<ContentApprovalStatus>(listQuery.ApprovalStatus, true, out var approvalFilter))
		{
			query = query.Where(r => r.ApprovalStatus == approvalFilter);
		}

		var totalCount = await query.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var reels = await ListSortApplicators
			.ApplyReelsSort(query, listQuery.SortBy, listQuery.SortDir)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(r => new
			{
				r.Id,
				r.Title,
				r.Description,
				videoUrl = r.VideoUrl,
				creatorId = r.CreatorId,
				creatorName = (r.Creator.FirstName ?? "") + " " + (r.Creator.LastName ?? ""),
				faces = r.ReelFaces.Select(rf => new { rf.FaceId, rf.Face.Title }),
				likesCount = r.Likes.Count,
				commentsCount = r.Comments.Count,
				approvalStatus = r.ApprovalStatus.ToString(),
				aiReviewStatus = r.AiReviewStatus.ToString(),
				creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(r.ApprovalStatus, r.AiReviewStatus),
				r.CreatedAt,
				r.UpdatedAt,
			})
			.ToListAsync(cancellationToken);

		return ListPaginationHelper.BuildEnvelope(reels, page, pageSize, totalCount, totalPages);
	}
}
