using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Stories;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Grid;

public sealed class StoryGridListService : IStoryGridListService
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IOptions<PerformanceOptions> _perfOptions;

	public StoryGridListService(
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

	public async Task<object> GetStoriesAsync(
		ClaimsPrincipal user,
		string? userId,
		StoryListQuery listQuery,
		CancellationToken cancellationToken = default)
	{
		var operatorInventory = _access.CanManageAllFaces(user);
		var page = listQuery.Page;
		var pageSize = listQuery.PageSize;

		IQueryable<Story> dbQuery = _context.Stories.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.GridSnapshot);

		if (!string.IsNullOrWhiteSpace(listQuery.CreatorId))
		{
			var creatorId = listQuery.CreatorId.Trim();
			dbQuery = dbQuery.Where(s => s.CreatorId == creatorId);
			if (listQuery.FaceId is > 0)
			{
				var scopedFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
				if (!operatorInventory &&
					!await StoryViewerRules.ViewerHasFaceMembershipAsync(_context, userId!, scopedFaceId, cancellationToken))
				{
					return ListPaginationHelper.BuildEnvelope(Array.Empty<object>(), page, pageSize, 0, 0);
				}

				dbQuery = dbQuery.Where(s =>
					!s.StoryFaces.Any() ||
					s.StoryFaces.Any(sf => sf.FaceId == scopedFaceId));
			}
		}
		else
		{
			var effectiveFaceId = _faceScope.ResolveDataFaceId(listQuery.FaceId);
			if (!operatorInventory &&
				!await StoryViewerRules.ViewerHasFaceMembershipAsync(_context, userId!, effectiveFaceId, cancellationToken))
			{
				return ListPaginationHelper.BuildEnvelope(Array.Empty<object>(), page, pageSize, 0, 0);
			}

			dbQuery = dbQuery.Where(s =>
				!s.StoryFaces.Any() ||
				s.StoryFaces.Any(sf => sf.FaceId == effectiveFaceId));
		}

		if (!operatorInventory)
		{
			var now = DateTime.UtcNow;
			dbQuery = dbQuery.Where(s =>
				s.State == StoryState.Published &&
				s.PublishedAt != null &&
				s.PublishedAt <= now &&
				s.ExpiresAt != null &&
				s.ExpiresAt > now);
		}
		else if (listQuery.IsPublished == true)
		{
			dbQuery = dbQuery.Where(s => s.State == StoryState.Published);
		}
		else if (listQuery.IsPublished == false)
		{
			dbQuery = dbQuery.Where(s => s.State != StoryState.Published);
		}

		if (!string.IsNullOrWhiteSpace(listQuery.Search))
		{
			var pattern = $"%{listQuery.Search.Trim()}%";
			dbQuery = dbQuery.Where(s => EF.Functions.ILike(s.Title, pattern));
		}

		var totalCount = await dbQuery.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var stories = await ListSortApplicators
			.ApplyStoriesSort(dbQuery, listQuery.SortBy, listQuery.SortDir)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(s => new
			{
				s.Id,
				s.Title,
				creatorId = s.CreatorId,
				creatorName = ((s.Creator.FirstName ?? "") + " " + (s.Creator.LastName ?? "")).Trim(),
				imageCount = s.Images.Count,
				isPublished = s.State == StoryState.Published,
				state = s.State.ToString(),
				s.PublishedAt,
				s.ExpiresAt,
				s.CreatedAt,
			})
			.ToListAsync(cancellationToken);

		return ListPaginationHelper.BuildEnvelope(stories, page, pageSize, totalCount, totalPages);
	}
}
