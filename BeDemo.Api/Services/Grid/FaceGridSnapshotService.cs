using System.Security.Claims;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Albums;
using BeDemo.Api.Models.Requests.Blogs;
using BeDemo.Api.Models.Requests.Reels;
using BeDemo.Api.Models.Requests.Stories;
using BeDemo.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.Grid;

public sealed class FaceGridSnapshotService : IFaceGridSnapshotService
{
	private readonly ApplicationDbContext _context;
	private readonly IFaceScopeContext _faceScope;
	private readonly IAccessEvaluator _access;
	private readonly IUploadSignedUrlService _uploadUrls;
	private readonly IAlbumGridListService _albums;
	private readonly IBlogGridListService _blogs;
	private readonly IReelGridListService _reels;
	private readonly IStoryGridListService _stories;
	private readonly IOptions<PerformanceOptions> _perfOptions;

	public FaceGridSnapshotService(
		ApplicationDbContext context,
		IFaceScopeContext faceScope,
		IAccessEvaluator access,
		IUploadSignedUrlService uploadUrls,
		IAlbumGridListService albums,
		IBlogGridListService blogs,
		IReelGridListService reels,
		IStoryGridListService stories,
		IOptions<PerformanceOptions> perfOptions)
	{
		_context = context;
		_faceScope = faceScope;
		_access = access;
		_uploadUrls = uploadUrls;
		_albums = albums;
		_blogs = blogs;
		_reels = reels;
		_stories = stories;
		_perfOptions = perfOptions;
	}

	public async Task<FaceGridSnapshotResult> GetSnapshotAsync(
		int faceId,
		ClaimsPrincipal user,
		string? userId,
		IReadOnlyList<string> blocks,
		int page,
		int pageSize,
		string requestScheme,
		string requestHost,
		CancellationToken cancellationToken = default)
	{
		var faceExists = await _context.Faces.AsNoTracking()
			.AnyAsync(f => f.Id == faceId, cancellationToken);
		if (!faceExists)
			return new FaceGridSnapshotResult { Status = FaceGridSnapshotStatus.NotFound };

		var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

		foreach (var block in blocks)
		{
			switch (block)
			{
				case GridBlockKeys.Albums:
					result[block] = await _albums.GetAlbumsAsync(
						user,
						userId,
						new AlbumListQuery { FaceId = faceId, Page = page, PageSize = pageSize },
						cancellationToken);
					break;
				case GridBlockKeys.Blogs:
					result[block] = await _blogs.GetBlogsAsync(
						user,
						userId,
						new BlogListQuery { FaceId = faceId, Page = page, PageSize = pageSize },
						cancellationToken);
					break;
				case GridBlockKeys.Reels:
					result[block] = await _reels.GetReelsAsync(
						user,
						userId,
						new ReelListQuery { FaceId = faceId, Page = page, PageSize = pageSize },
						cancellationToken);
					break;
				case GridBlockKeys.Stories:
					result[block] = await _stories.GetStoriesAsync(
						user,
						userId,
						new StoryListQuery { FaceId = faceId, Page = page, PageSize = pageSize },
						cancellationToken);
					break;
				case GridBlockKeys.ChatRooms:
					{
						var chatOutcome = await GetChatRoomsBlockAsync(faceId, userId!, page, pageSize, cancellationToken);
						if (chatOutcome.Status != FaceGridSnapshotStatus.Success)
							return new FaceGridSnapshotResult { Status = chatOutcome.Status };
						result[block] = chatOutcome.Body!;
						break;
					}
				case GridBlockKeys.VideoLounges:
					{
						var loungeOutcome = await GetVideoLoungesBlockAsync(faceId, userId!, page, pageSize, cancellationToken);
						if (loungeOutcome.Status != FaceGridSnapshotStatus.Success)
							return new FaceGridSnapshotResult { Status = loungeOutcome.Status };
						result[block] = loungeOutcome.Body!;
						break;
					}
				case GridBlockKeys.Profiles:
					{
						var profilesOutcome = await GetProfilesBlockAsync(
							faceId,
							user,
							userId,
							page,
							pageSize,
							requestScheme,
							requestHost,
							cancellationToken);
						if (profilesOutcome.Status != FaceGridSnapshotStatus.Success)
							return new FaceGridSnapshotResult { Status = profilesOutcome.Status };
						result[block] = profilesOutcome.Body!;
						break;
					}
				case GridBlockKeys.WallTickets:
					{
						var ticketsOutcome = await GetWallTicketsBlockAsync(faceId, userId!, page, pageSize, cancellationToken);
						if (ticketsOutcome.Status != FaceGridSnapshotStatus.Success)
							return new FaceGridSnapshotResult { Status = ticketsOutcome.Status };
						result[block] = ticketsOutcome.Body!;
						break;
					}
			}
		}

		return new FaceGridSnapshotResult
		{
			Status = FaceGridSnapshotStatus.Success,
			Blocks = result,
		};
	}

	private sealed record BlockOutcome(FaceGridSnapshotStatus Status, object? Body);

	private async Task<BlockOutcome> GetChatRoomsBlockAsync(
		int faceId,
		string userId,
		int page,
		int pageSize,
		CancellationToken cancellationToken)
	{
		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, userId, faceId, cancellationToken);

		IQueryable<FaceChatRoom> roomQuery = _context.FaceChatRooms.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.GridSnapshot)
			.Where(r => r.FaceId == faceId);

		var totalCount = await roomQuery.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var rooms = await ListSortApplicators
			.ApplyFaceChatRoomsSort(roomQuery, sortBy: null, sortDir: null)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var ids = rooms.Select(r => r.Id).ToList();
		var memberCounts = await _context.FaceChatRoomMembers
			.AsNoTracking()
			.Where(m => ids.Contains(m.FaceChatRoomId))
			.GroupBy(m => m.FaceChatRoomId)
			.Select(g => new { RoomId = g.Key, C = g.Count() })
			.ToDictionaryAsync(x => x.RoomId, x => x.C, cancellationToken);

		var myMemberships = (await _context.FaceChatRoomMembers
			.AsNoTracking()
			.Where(m => m.UserId == userId && ids.Contains(m.FaceChatRoomId))
			.Select(m => m.FaceChatRoomId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var pending = (await _context.FaceChatRoomJoinRequests
			.AsNoTracking()
			.Where(j => j.UserId == userId && j.Status == FaceChatRoomJoinRequestStatus.Pending && ids.Contains(j.FaceChatRoomId))
			.Select(j => j.FaceChatRoomId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var list = rooms.Select(r =>
		{
			var mc = memberCounts.GetValueOrDefault(r.Id);
			var member = myMemberships.Contains(r.Id);
			var canPart = !isHost;
			return ChatRoomDto(r, isHost, canPart, member, pending.Contains(r.Id), mc, messageCount: 0, pendingJoinRequestCount: 0);
		}).ToList();

		return new BlockOutcome(
			FaceGridSnapshotStatus.Success,
			ListPaginationHelper.BuildEnvelope(list, page, pageSize, totalCount, totalPages));
	}

	private static object ChatRoomDto(
		FaceChatRoom r,
		bool isHostViewer,
		bool canParticipate,
		bool isMember,
		bool hasPendingRequest,
		int memberCount,
		int messageCount,
		int pendingJoinRequestCount) =>
		new
		{
			r.Id,
			r.FaceId,
			r.Title,
			r.Description,
			r.IsPublic,
			r.IsSystemManaged,
			r.CreatorUserId,
			r.CreatedAt,
			r.UpdatedAt,
			r.LastMessageAt,
			memberCount,
			messageCount,
			pendingJoinRequestCount,
			isHostViewer,
			canParticipate,
			isMember,
			hasPendingRequest,
		};

	private async Task<BlockOutcome> GetVideoLoungesBlockAsync(
		int faceId,
		string userId,
		int page,
		int pageSize,
		CancellationToken cancellationToken)
	{
		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, userId, faceId, cancellationToken);

		IQueryable<FaceVideoLounge> loungeQuery = _context.FaceVideoLounges.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.GridSnapshot)
			.Where(r => r.FaceId == faceId);

		var totalCount = await loungeQuery.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var lounges = await ListSortApplicators
			.ApplyFaceVideoLoungesSort(loungeQuery, sortBy: null, sortDir: null)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var ids = lounges.Select(r => r.Id).ToList();
		var memberCounts = await _context.FaceVideoLoungeMembers
			.AsNoTracking()
			.Where(m => ids.Contains(m.FaceVideoLoungeId))
			.GroupBy(m => m.FaceVideoLoungeId)
			.Select(g => new { LoungeId = g.Key, C = g.Count() })
			.ToDictionaryAsync(x => x.LoungeId, x => x.C, cancellationToken);

		var sessionByLounge = await _context.FaceVideoLoungeSessions.AsNoTracking()
			.Where(s => ids.Contains(s.FaceVideoLoungeId) && s.EndedAt == null)
			.ToDictionaryAsync(s => s.FaceVideoLoungeId, s => s.Id, cancellationToken);

		// Batch all participant counts into one grouped query instead of one CountAsync per lounge (X10 N+1).
		Dictionary<int, int> liveCounts;
		if (sessionByLounge.Count == 0)
		{
			liveCounts = [];
		}
		else
		{
			var sessionIds = sessionByLounge.Values.ToList();
			var countsBySession = await _context.FaceVideoLoungeSessionParticipants.AsNoTracking()
				.Where(p => sessionIds.Contains(p.FaceVideoLoungeSessionId) && p.LeftAt == null && p.IsListedInPublicRoster)
				.GroupBy(p => p.FaceVideoLoungeSessionId)
				.Select(g => new { g.Key, C = g.Count() })
				.ToDictionaryAsync(x => x.Key, x => x.C, cancellationToken);
			liveCounts = sessionByLounge.ToDictionary(kv => kv.Key, kv => countsBySession.GetValueOrDefault(kv.Value));
		}

		var myMemberships = (await _context.FaceVideoLoungeMembers
			.AsNoTracking()
			.Where(m => m.UserId == userId && ids.Contains(m.FaceVideoLoungeId))
			.Select(m => m.FaceVideoLoungeId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var pending = (await _context.FaceVideoLoungeJoinRequests
			.AsNoTracking()
			.Where(j => j.UserId == userId && j.Status == FaceVideoLoungeJoinRequestStatus.Pending && ids.Contains(j.FaceVideoLoungeId))
			.Select(j => j.FaceVideoLoungeId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var list = lounges.Select(r =>
		{
			var mc = memberCounts.GetValueOrDefault(r.Id);
			var member = myMemberships.Contains(r.Id);
			var hasLive = sessionByLounge.ContainsKey(r.Id);
			var liveN = liveCounts.GetValueOrDefault(r.Id);
			return VideoLoungeDto(r, isHost, canConnect: !isHost && member, member, pending.Contains(r.Id), mc, hasLive, liveN);
		}).ToList();

		return new BlockOutcome(
			FaceGridSnapshotStatus.Success,
			ListPaginationHelper.BuildEnvelope(list, page, pageSize, totalCount, totalPages));
	}

	private static object VideoLoungeDto(
		FaceVideoLounge r,
		bool isHostViewer,
		bool canConnect,
		bool isMember,
		bool hasPendingRequest,
		int memberCount,
		bool hasLiveSession,
		int liveParticipantCount) =>
		new
		{
			r.Id,
			r.FaceId,
			r.Title,
			r.Description,
			r.IsPublic,
			r.IsSystemManaged,
			r.CreatorUserId,
			r.MaxParticipants,
			r.CreatedAt,
			r.UpdatedAt,
			memberCount,
			hasLiveSession,
			liveParticipantCount,
			isHostViewer,
			canConnect,
			isMember,
			hasPendingRequest,
		};

	private async Task<BlockOutcome> GetProfilesBlockAsync(
		int faceId,
		ClaimsPrincipal user,
		string? userId,
		int page,
		int pageSize,
		string requestScheme,
		string requestHost,
		CancellationToken cancellationToken)
	{
		var face = await _context.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == faceId, cancellationToken);
		if (face == null)
			return new BlockOutcome(FaceGridSnapshotStatus.NotFound, null);

		var operatorInventory = _access.CanManageAllFaces(user);
		if (!operatorInventory &&
			!await FaceVisibilityAccess.CanViewFaceProfileContentAsync(_context, face, userId, cancellationToken))
			return new BlockOutcome(FaceGridSnapshotStatus.Forbidden, null);

		var hostName = UserRole.FaceRoleNames.FaceHost;
		var baseQuery =
			from ufp in _context.UserFaceProfiles.AsNoTracking()
			join up in _context.UserProfiles.AsNoTracking() on ufp.UserProfileId equals up.Id
			where ufp.FaceId == faceId
			where _context.UserFaceRoles.Any(ufr =>
				ufr.UserId == up.UserId &&
				ufr.FaceId == faceId &&
				_context.UserRoles.Any(ur =>
					ur.Id == ufr.UserRoleId &&
					ur.Scope == RoleScope.Face &&
					ur.Name != hostName))
			select new { ufp, up };

		var ordered = baseQuery.OrderBy(x => x.ufp.DisplayName ?? x.up.Nickname);
		var totalCount = await ordered.CountAsync(cancellationToken);
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);
		page = clampedPage;

		var pageRows = await ordered
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new
			{
				ProfileId = x.ufp.Id,
				x.up.UserId,
				x.ufp.DisplayName,
				x.up.Nickname,
				FaceAvatar = x.ufp.AvatarUrl,
				GlobalAvatar = x.up.AvatarUrl,
			})
			.ToListAsync(cancellationToken);

		var profileIds = pageRows.Select(r => r.ProfileId).ToList();
		var commentCounts = await _context.UserFaceProfileComments.AsNoTracking()
			.Where(c => profileIds.Contains(c.UserFaceProfileId))
			.GroupBy(c => c.UserFaceProfileId)
			.Select(g => new { g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
		var likeCounts = await _context.UserFaceProfileLikes.AsNoTracking()
			.Where(l => profileIds.Contains(l.UserFaceProfileId))
			.GroupBy(l => l.UserFaceProfileId)
			.Select(g => new { g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
		var reviewCounts = face.AllowRecensions
			? await _context.UserFaceProfileReviews.AsNoTracking()
				.Where(r => profileIds.Contains(r.UserFaceProfileId))
				.GroupBy(r => r.UserFaceProfileId)
				.Select(g => new { g.Key, Count = g.Count() })
				.ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
			: new Dictionary<int, int>();

		var bannedSet = (await _context.UserFaceModerations.AsNoTracking()
			.Where(m => m.FaceId == faceId && m.LiftedAt == null)
			.Select(m => m.UserId)
			.ToListAsync(cancellationToken)).ToHashSet();

		var items = pageRows.Select(row =>
		{
			var display = row.DisplayName?.Trim();
			if (string.IsNullOrEmpty(display))
				display = row.Nickname;
			var avatar = !string.IsNullOrWhiteSpace(row.FaceAvatar) ? row.FaceAvatar : row.GlobalAvatar;
			return new
			{
				userId = row.UserId,
				displayName = display,
				avatarUrl = _uploadUrls.ToAbsoluteSignedUrl(avatar, requestScheme, requestHost),
				commentsCount = commentCounts.GetValueOrDefault(row.ProfileId),
				likesCount = likeCounts.GetValueOrDefault(row.ProfileId),
				reviewsCount = face.AllowRecensions ? reviewCounts.GetValueOrDefault(row.ProfileId) : 0,
				isFaceBanned = bannedSet.Contains(row.UserId),
			};
		}).ToList();

		return new BlockOutcome(
			FaceGridSnapshotStatus.Success,
			ListPaginationHelper.BuildEnvelope(items, page, pageSize, totalCount, totalPages));
	}

	private async Task<BlockOutcome> GetWallTicketsBlockAsync(
		int faceId,
		string userId,
		int page,
		int pageSize,
		CancellationToken cancellationToken)
	{
		var isHost = await FaceChatRoomAuth.IsHostInFaceAsync(_context, userId, faceId, cancellationToken);

		var query = _context.FaceWallTickets
			.AsNoTracking()
			.TagIfEnabled(_perfOptions, EfQueryTags.GridSnapshot)
			.Include(t => t.Creator)
			.Where(t => t.FaceId == faceId);

		var total = await query.CountAsync(cancellationToken);

		var items = await query
			.OrderByDescending(t => t.CreatedAt)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(t => new
			{
				t.Id,
				t.Title,
				descriptionPreview = t.Description.Length > 200 ? t.Description.Substring(0, 200) + "…" : t.Description,
				status = WallTicketStatusString(t.Status),
				creatorId = t.CreatorUserId,
				creatorName = ((t.Creator.FirstName ?? "") + " " + (t.Creator.LastName ?? "")).Trim(),
				likesCount = t.Likes.Count,
				commentsCount = t.Comments.Count,
				isLikedByMe = t.Likes.Any(l => l.UserId == userId),
				isAuthor = t.CreatorUserId == userId,
				t.CreatedAt,
				canInteract = t.Status == FaceWallTicketStatus.Active && !isHost,
				isHostViewer = isHost,
			})
			.ToListAsync(cancellationToken);

		return new BlockOutcome(
			FaceGridSnapshotStatus.Success,
			new
			{
				items,
				isHostViewer = isHost,
				page,
				pageSize,
				totalCount = total,
				totalPages = (int)Math.Ceiling(total / (double)pageSize),
			});
	}

	private static string WallTicketStatusString(FaceWallTicketStatus s) =>
		s switch
		{
			FaceWallTicketStatus.Active => "active",
			FaceWallTicketStatus.Approved => "approved",
			FaceWallTicketStatus.Denied => "denied",
			_ => "active",
		};
}
