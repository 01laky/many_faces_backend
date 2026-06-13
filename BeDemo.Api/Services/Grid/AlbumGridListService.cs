using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Albums;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Grid;

public sealed class AlbumGridListService : IAlbumGridListService
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IOptions<PerformanceOptions> _perfOptions;

	public AlbumGridListService(
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

	public async Task<object> GetAlbumsAsync(
		ClaimsPrincipal user,
		string? userId,
		AlbumListQuery listQuery,
		CancellationToken cancellationToken = default)
	{
		var operatorInventory = _access.CanManageAllFaces(user);
		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;

		IQueryable<Album> query = _context.Albums.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.GridSnapshot);
		// Empty (never a real CreatorId) when unauthenticated, so the own-private-album rule matches nothing
		// and an anonymous viewer sees only public approved albums (also clears the CS8604 nullable warning).
		query = OperatorContentListFilters.ApplyAlbumPortalVisibility(query, operatorInventory, userId ?? string.Empty);

		if (!string.IsNullOrWhiteSpace(listQuery.CreatorId))
		{
			var creatorId = listQuery.CreatorId.Trim();
			query = query.Where(a => a.CreatorId == creatorId);
			if (listQuery.FaceId is > 0)
			{
				var scopedFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
				query = query.Where(a => a.AlbumFaces.Any(af => af.FaceId == scopedFaceId));
			}
		}
		else
		{
			var effectiveFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
			query = query.Where(a => a.AlbumFaces.Any(af => af.FaceId == effectiveFaceId));
		}

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var pattern = $"%{listQuery.Search.Trim()}%";
			query = query.Where(a =>
				EF.Functions.ILike(a.Title, pattern) ||
				(a.Description != null && EF.Functions.ILike(a.Description, pattern)));
		}

		if (!string.IsNullOrWhiteSpace(listQuery.ApprovalStatus) &&
			Enum.TryParse<ContentApprovalStatus>(listQuery.ApprovalStatus, true, out var approvalFilter))
		{
			query = query.Where(a => a.ApprovalStatus == approvalFilter);
		}

		if (!string.IsNullOrWhiteSpace(listQuery.MediaType) &&
			int.TryParse(listQuery.MediaType, out var mediaTypeInt) &&
			Enum.IsDefined(typeof(MediaTypeEnum), mediaTypeInt))
		{
			var mediaType = (MediaTypeEnum)mediaTypeInt;
			query = query.Where(a => a.MediaType == mediaType);
		}

		if (!string.IsNullOrWhiteSpace(listQuery.AlbumType) &&
			int.TryParse(listQuery.AlbumType, out var albumTypeInt) &&
			Enum.IsDefined(typeof(AlbumTypeEnum), albumTypeInt))
		{
			var albumType = (AlbumTypeEnum)albumTypeInt;
			query = query.Where(a => a.AlbumType == albumType);
		}

		var totalCount = await query.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var albums = await ListSortApplicators
			.ApplyAlbumsSort(query, listQuery.SortBy, listQuery.SortDir)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(a => new
			{
				a.Id,
				a.Title,
				a.Description,
				albumType = (int)a.AlbumType,
				mediaType = (int)a.MediaType,
				creatorId = a.CreatorId,
				creatorName = (a.Creator.FirstName ?? "") + " " + (a.Creator.LastName ?? ""),
				faces = a.AlbumFaces.Select(af => new { af.FaceId, af.Face.Title }),
				likesCount = a.Likes.Count,
				commentsCount = a.Comments.Count,
				mediaCount = a.MediaItems.Count,
				approvalStatus = a.ApprovalStatus.ToString(),
				aiReviewStatus = a.AiReviewStatus.ToString(),
				creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(a.ApprovalStatus, a.AiReviewStatus),
				a.CreatedAt,
				a.UpdatedAt,
			})
			.ToListAsync(cancellationToken);

		return ListPaginationHelper.BuildEnvelope(albums, page, pageSize, totalCount, totalPages);
	}
}
