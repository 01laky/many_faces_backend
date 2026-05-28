using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Blogs;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Grid;

public sealed class BlogGridListService : IBlogGridListService
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IOptions<PerformanceOptions> _perfOptions;

	public BlogGridListService(
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

	public async Task<object> GetBlogsAsync(
		ClaimsPrincipal user,
		string? userId,
		BlogListQuery listQuery,
		CancellationToken cancellationToken = default)
	{
		var operatorInventory = _access.CanManageAllFaces(user);
		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;

		IQueryable<Blog> query = _context.Blogs.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.GridSnapshot);
		query = OperatorContentListFilters.ApplyBlogPortalVisibility(query, operatorInventory);

		if (!string.IsNullOrWhiteSpace(listQuery.CreatorId))
		{
			var creatorId = listQuery.CreatorId.Trim();
			query = query.Where(b => b.CreatorId == creatorId);
			if (listQuery.FaceId is > 0)
			{
				var scopedFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
				query = query.Where(b => b.FaceId == scopedFaceId);
			}
		}
		else
		{
			var effectiveFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
			query = query.Where(b => b.FaceId == effectiveFaceId);
		}

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var pattern = $"%{listQuery.Search.Trim()}%";
			query = query.Where(b =>
				EF.Functions.ILike(b.Title, pattern) ||
				EF.Functions.ILike(b.Content, pattern));
		}

		if (!string.IsNullOrWhiteSpace(listQuery.ApprovalStatus) &&
			Enum.TryParse<ContentApprovalStatus>(listQuery.ApprovalStatus, true, out var approvalFilter))
		{
			query = query.Where(b => b.ApprovalStatus == approvalFilter);
		}

		var totalCount = await query.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var blogs = await ListSortApplicators
			.ApplyBlogsSort(query, listQuery.SortBy, listQuery.SortDir)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(b => new
			{
				b.Id,
				b.Title,
				b.Content,
				b.FaceId,
				faceTitle = b.Face.Title,
				creatorId = b.CreatorId,
				creatorName = (b.Creator.FirstName ?? "") + " " + (b.Creator.LastName ?? ""),
				images = b.Images.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.ImageUrl, i.SortOrder }),
				imageCount = b.Images.Count,
				likesCount = b.Likes.Count,
				commentsCount = b.Comments.Count,
				approvalStatus = b.ApprovalStatus.ToString(),
				aiReviewStatus = b.AiReviewStatus.ToString(),
				creatorStatusLabel = ContentModerationHelpers.CreatorStatusLabel(b.ApprovalStatus, b.AiReviewStatus),
				b.CreatedAt,
				b.UpdatedAt,
			})
			.ToListAsync(cancellationToken);

		return ListPaginationHelper.BuildEnvelope(blogs, page, pageSize, totalCount, totalPages);
	}
}
