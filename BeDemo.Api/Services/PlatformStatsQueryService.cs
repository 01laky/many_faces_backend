using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.DTOs.OperatorAi;
using BeDemo.Api.Utils;

namespace BeDemo.Api.Services;

/// <summary>
/// Centralizes read-only aggregate queries for dashboard and AI context so <see cref="Controllers.StatsController"/>
/// and SignalR hubs stay thin.
/// </summary>
public interface IPlatformStatsQueryService
{
	Task<AdminDashboardSummaryDto> GetOperatorDashboardSummaryAsync(CancellationToken cancellationToken = default);

	Task<PublicStatsSnapshotDto> GetPublicSnapshotAsync(CancellationToken cancellationToken = default);

	/// <summary>Last 7 UTC days of daily counts for key metrics (operator AI context).</summary>
	Task<OperatorAiTimeseriesHintsDto> GetOperatorAiTimeseriesHintsAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class PlatformStatsQueryService : IPlatformStatsQueryService
{
	// Used for the timeseries method which runs 3 sequential queries (acceptable; no N+1).
	private readonly ApplicationDbContext _context;

	// Used for parallel count batches (X10): each RunAsync call gets an independent context so counts can
	// execute concurrently without hitting the single-context concurrency restriction in EF Core.
	private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

	public PlatformStatsQueryService(
		ApplicationDbContext context,
		IDbContextFactory<ApplicationDbContext> dbContextFactory)
	{
		_context = context;
		_dbContextFactory = dbContextFactory;
	}

	/// <summary>
	/// Executes <paramref name="fn"/> on a fresh <see cref="ApplicationDbContext"/> (safe for concurrent use).
	/// </summary>
	private async Task<T> RunAsync<T>(Func<ApplicationDbContext, CancellationToken, Task<T>> fn, CancellationToken cancellationToken)
	{
		await using var ctx = _dbContextFactory.CreateDbContext();
		return await fn(ctx, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<PublicStatsSnapshotDto> GetPublicSnapshotAsync(CancellationToken cancellationToken = default)
	{
		// Fire all 14 counts in parallel (X10: eliminates sequential round-trips on this public snapshot path).
		var usersT = RunAsync((c, ct) => c.Users.AsNoTracking().CountAsync(ct), cancellationToken);
		var facesT = RunAsync((c, ct) => c.Faces.AsNoTracking().CountAsync(ct), cancellationToken);
		var pagesT = RunAsync((c, ct) => c.Pages.AsNoTracking().CountAsync(ct), cancellationToken);
		var friendshipsT = RunAsync((c, ct) => c.Friendships.AsNoTracking().CountAsync(ct), cancellationToken);
		var friendReqPendingT = RunAsync((c, ct) => c.FriendRequests.AsNoTracking().CountAsync(r => r.Status == FriendRequestStatus.Pending, ct), cancellationToken);
		var messagesT = RunAsync((c, ct) => c.Messages.AsNoTracking().CountAsync(ct), cancellationToken);
		var albumsT = RunAsync((c, ct) => c.Albums.AsNoTracking().CountAsync(ct), cancellationToken);
		var blogsT = RunAsync((c, ct) => c.Blogs.AsNoTracking().CountAsync(ct), cancellationToken);
		var reelsT = RunAsync((c, ct) => c.Reels.AsNoTracking().CountAsync(ct), cancellationToken);
		var storiesT = RunAsync((c, ct) => c.Stories.AsNoTracking().CountAsync(ct), cancellationToken);
		var storyViewsT = RunAsync((c, ct) => c.StoryViews.AsNoTracking().CountAsync(ct), cancellationToken);
		var wallTicketsT = RunAsync((c, ct) => c.FaceWallTickets.AsNoTracking().CountAsync(ct), cancellationToken);
		var chatRoomsT = RunAsync((c, ct) => c.FaceChatRooms.AsNoTracking().CountAsync(ct), cancellationToken);
		var chatRoomMessagesT = RunAsync((c, ct) => c.FaceChatRoomMessages.AsNoTracking().CountAsync(ct), cancellationToken);
		var videoLoungesT = RunAsync((c, ct) => c.FaceVideoLounges.AsNoTracking().CountAsync(ct), cancellationToken);

		await Task.WhenAll(
			usersT, facesT, pagesT, friendshipsT, friendReqPendingT, messagesT,
			albumsT, blogsT, reelsT, storiesT, storyViewsT,
			wallTicketsT, chatRoomsT, chatRoomMessagesT, videoLoungesT);

		return new PublicStatsSnapshotDto
		{
			UsersCount = usersT.Result,
			FacesCount = facesT.Result,
			PagesCount = pagesT.Result,
			FriendshipsCount = friendshipsT.Result,
			FriendRequestsPendingCount = friendReqPendingT.Result,
			MessagesCount = messagesT.Result,
			AlbumsCount = albumsT.Result,
			BlogsCount = blogsT.Result,
			ReelsCount = reelsT.Result,
			StoriesCount = storiesT.Result,
			StoryViewsCount = storyViewsT.Result,
			FaceWallTicketsCount = wallTicketsT.Result,
			FaceChatRoomsCount = chatRoomsT.Result,
			FaceChatRoomMessagesCount = chatRoomMessagesT.Result,
			FaceVideoLoungesCount = videoLoungesT.Result,
		};
	}

	/// <inheritdoc />
	public async Task<AdminDashboardSummaryDto> GetOperatorDashboardSummaryAsync(CancellationToken cancellationToken = default)
	{
		// Fire all ~35 counts in parallel on independent contexts (X10: eliminates sequential round-trips).
		var usersT = RunAsync((c, ct) => c.Users.AsNoTracking().CountAsync(ct), cancellationToken);
		var friendReqT = RunAsync((c, ct) => c.FriendRequests.AsNoTracking().CountAsync(r => r.Status == FriendRequestStatus.Pending, ct), cancellationToken);
		var messagesT = RunAsync((c, ct) => c.Messages.AsNoTracking().CountAsync(ct), cancellationToken);
		var facesT = RunAsync((c, ct) => c.Faces.AsNoTracking().CountAsync(ct), cancellationToken);
		var pagesT = RunAsync((c, ct) => c.Pages.AsNoTracking().CountAsync(ct), cancellationToken);
		var pageCompsT = RunAsync((c, ct) => c.PageComponents.AsNoTracking().CountAsync(ct), cancellationToken);
		var pageRouteTransT = RunAsync((c, ct) => c.PageRouteTranslations.AsNoTracking().CountAsync(ct), cancellationToken);
		var friendshipsT = RunAsync((c, ct) => c.Friendships.AsNoTracking().CountAsync(ct), cancellationToken);
		var friendReqAcceptedT = RunAsync((c, ct) => c.FriendRequests.AsNoTracking().CountAsync(r => r.Status == FriendRequestStatus.Accepted, ct), cancellationToken);
		var friendReqRejectedT = RunAsync((c, ct) => c.FriendRequests.AsNoTracking().CountAsync(r => r.Status == FriendRequestStatus.Rejected, ct), cancellationToken);
		var userFollowsT = RunAsync((c, ct) => c.UserFollows.AsNoTracking().CountAsync(ct), cancellationToken);
		var userBlocksT = RunAsync((c, ct) => c.UserBlocks.AsNoTracking().CountAsync(ct), cancellationToken);
		var msgsPendingT = RunAsync((c, ct) => c.Messages.AsNoTracking().CountAsync(m => m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending, ct), cancellationToken);
		var notificationsT = RunAsync((c, ct) => c.Notifications.AsNoTracking().CountAsync(ct), cancellationToken);
		var albumsT = RunAsync((c, ct) => c.Albums.AsNoTracking().CountAsync(ct), cancellationToken);
		var blogsT = RunAsync((c, ct) => c.Blogs.AsNoTracking().CountAsync(ct), cancellationToken);
		var reelsT = RunAsync((c, ct) => c.Reels.AsNoTracking().CountAsync(ct), cancellationToken);
		var storiesT = RunAsync((c, ct) => c.Stories.AsNoTracking().CountAsync(ct), cancellationToken);
		var storyViewsT = RunAsync((c, ct) => c.StoryViews.AsNoTracking().CountAsync(ct), cancellationToken);
		var chatRoomsT = RunAsync((c, ct) => c.FaceChatRooms.AsNoTracking().CountAsync(ct), cancellationToken);
		var chatRoomMembersT = RunAsync((c, ct) => c.FaceChatRoomMembers.AsNoTracking().CountAsync(ct), cancellationToken);
		var chatRoomMsgsT = RunAsync((c, ct) => c.FaceChatRoomMessages.AsNoTracking().CountAsync(ct), cancellationToken);
		var chatRoomJoinReqT = RunAsync((c, ct) => c.FaceChatRoomJoinRequests.AsNoTracking().CountAsync(j => j.Status == FaceChatRoomJoinRequestStatus.Pending, ct), cancellationToken);
		var videoLoungesT = RunAsync((c, ct) => c.FaceVideoLounges.AsNoTracking().CountAsync(ct), cancellationToken);
		var videoLoungeMembersT = RunAsync((c, ct) => c.FaceVideoLoungeMembers.AsNoTracking().CountAsync(ct), cancellationToken);
		var videoLoungeSessionsT = RunAsync((c, ct) => c.FaceVideoLoungeSessions.AsNoTracking().CountAsync(s => s.EndedAt == null, ct), cancellationToken);
		var wallTicketsT = RunAsync((c, ct) => c.FaceWallTickets.AsNoTracking().CountAsync(ct), cancellationToken);
		var wallTicketsByStatusT = RunAsync(BuildFaceWallTicketStatusCountsAsync, cancellationToken);
		var wallTicketCommentsT = RunAsync((c, ct) => c.FaceWallTicketComments.AsNoTracking().CountAsync(ct), cancellationToken);
		var wallTicketLikesT = RunAsync((c, ct) => c.FaceWallTicketLikes.AsNoTracking().CountAsync(ct), cancellationToken);
		var ufpT = RunAsync((c, ct) => c.UserFaceProfiles.AsNoTracking().CountAsync(ct), cancellationToken);
		var ufpLikesT = RunAsync((c, ct) => c.UserFaceProfileLikes.AsNoTracking().CountAsync(ct), cancellationToken);
		var ufpCommentsT = RunAsync((c, ct) => c.UserFaceProfileComments.AsNoTracking().CountAsync(ct), cancellationToken);
		var ufpReviewsT = RunAsync((c, ct) => c.UserFaceProfileReviews.AsNoTracking().CountAsync(ct), cancellationToken);
		var albumCommentsT = RunAsync((c, ct) => c.AlbumComments.AsNoTracking().CountAsync(ct), cancellationToken);
		var blogCommentsT = RunAsync((c, ct) => c.BlogComments.AsNoTracking().CountAsync(ct), cancellationToken);
		var reelCommentsT = RunAsync((c, ct) => c.ReelComments.AsNoTracking().CountAsync(ct), cancellationToken);
		var storyCommentsT = RunAsync((c, ct) => c.StoryComments.AsNoTracking().CountAsync(ct), cancellationToken);
		var albumLikesT = RunAsync((c, ct) => c.AlbumLikes.AsNoTracking().CountAsync(ct), cancellationToken);
		var blogLikesT = RunAsync((c, ct) => c.BlogLikes.AsNoTracking().CountAsync(ct), cancellationToken);
		var reelLikesT = RunAsync((c, ct) => c.ReelLikes.AsNoTracking().CountAsync(ct), cancellationToken);
		var storyLikesT = RunAsync((c, ct) => c.StoryLikes.AsNoTracking().CountAsync(ct), cancellationToken);
		var aiReviewJobsT = RunAsync((c, ct) => c.AiReviewJobs.AsNoTracking().CountAsync(ct), cancellationToken);
		var contentModEventsT = RunAsync((c, ct) => c.ContentModerationEvents.AsNoTracking().CountAsync(ct), cancellationToken);
		var oauthClientsT = RunAsync((c, ct) => c.OAuthClients.AsNoTracking().CountAsync(ct), cancellationToken);

		await Task.WhenAll(
			usersT, friendReqT, messagesT,
			facesT, pagesT, pageCompsT, pageRouteTransT,
			friendshipsT, friendReqAcceptedT, friendReqRejectedT, userFollowsT, userBlocksT,
			msgsPendingT, notificationsT,
			albumsT, blogsT, reelsT, storiesT, storyViewsT,
			chatRoomsT, chatRoomMembersT, chatRoomMsgsT, chatRoomJoinReqT,
			videoLoungesT, videoLoungeMembersT, videoLoungeSessionsT,
			wallTicketsT, wallTicketsByStatusT, wallTicketCommentsT, wallTicketLikesT,
			ufpT, ufpLikesT, ufpCommentsT, ufpReviewsT,
			albumCommentsT, blogCommentsT, reelCommentsT, storyCommentsT,
			albumLikesT, blogLikesT, reelLikesT, storyLikesT,
			aiReviewJobsT, contentModEventsT, oauthClientsT);

		return new AdminDashboardSummaryDto
		{
			UsersCount = usersT.Result,
			FriendRequestsCount = friendReqT.Result,
			MessagesCount = messagesT.Result,

			FacesCount = facesT.Result,
			PagesCount = pagesT.Result,
			PageComponentsCount = pageCompsT.Result,
			PageRouteTranslationsCount = pageRouteTransT.Result,

			FriendshipsCount = friendshipsT.Result,
			FriendRequestsAcceptedCount = friendReqAcceptedT.Result,
			FriendRequestsRejectedCount = friendReqRejectedT.Result,
			UserFollowsCount = userFollowsT.Result,
			UserBlocksCount = userBlocksT.Result,

			MessagesPendingRequestCount = msgsPendingT.Result,

			NotificationsCount = notificationsT.Result,

			AlbumsCount = albumsT.Result,
			BlogsCount = blogsT.Result,
			ReelsCount = reelsT.Result,
			StoriesCount = storiesT.Result,
			StoryViewsCount = storyViewsT.Result,

			FaceChatRoomsCount = chatRoomsT.Result,
			FaceChatRoomMembersCount = chatRoomMembersT.Result,
			FaceChatRoomMessagesCount = chatRoomMsgsT.Result,
			FaceChatRoomJoinRequestsPendingCount = chatRoomJoinReqT.Result,

			FaceVideoLoungesCount = videoLoungesT.Result,
			FaceVideoLoungeMembersCount = videoLoungeMembersT.Result,
			FaceVideoLoungeLiveSessionsCount = videoLoungeSessionsT.Result,

			FaceWallTicketsCount = wallTicketsT.Result,
			FaceWallTicketsByStatus = wallTicketsByStatusT.Result,
			FaceWallTicketCommentsCount = wallTicketCommentsT.Result,
			FaceWallTicketLikesCount = wallTicketLikesT.Result,

			UserFaceProfilesCount = ufpT.Result,
			UserFaceProfileLikesCount = ufpLikesT.Result,
			UserFaceProfileCommentsCount = ufpCommentsT.Result,
			UserFaceProfileReviewsCount = ufpReviewsT.Result,

			AlbumCommentsCount = albumCommentsT.Result,
			BlogCommentsCount = blogCommentsT.Result,
			ReelCommentsCount = reelCommentsT.Result,
			StoryCommentsCount = storyCommentsT.Result,
			AlbumLikesCount = albumLikesT.Result,
			BlogLikesCount = blogLikesT.Result,
			ReelLikesCount = reelLikesT.Result,
			StoryLikesCount = storyLikesT.Result,

			AiReviewJobsCount = aiReviewJobsT.Result,
			ContentModerationEventsCount = contentModEventsT.Result,

			OAuthClientsCount = oauthClientsT.Result,
		};
	}

	/// <inheritdoc />
	public async Task<OperatorAiTimeseriesHintsDto> GetOperatorAiTimeseriesHintsAsync(
		CancellationToken cancellationToken = default)
	{
		var toUtc = DateTime.UtcNow;
		var fromUtc = toUtc.Date.AddDays(-6);
		const string bucket = "day";

		async Task<IReadOnlyList<OperatorAiTimeseriesBucketDto>> SeriesAsync(
			IQueryable<DateTime> query)
		{
			var timestamps = await query.ToListAsync(cancellationToken);
			return StatsTimeseriesBucketing
				.BucketizeUtc(timestamps, fromUtc, toUtc, bucket)
				.Select(b => new OperatorAiTimeseriesBucketDto
				{
					PeriodStartUtc = b.PeriodStartUtc,
					Count = b.Count,
				})
				.ToList();
		}

		var series = new Dictionary<string, IReadOnlyList<OperatorAiTimeseriesBucketDto>>
		{
			["users"] = await SeriesAsync(
				_context.Users.AsNoTracking()
					.Where(u => u.CreatedAt >= fromUtc && u.CreatedAt <= toUtc)
					.Select(u => u.CreatedAt)),
			["messages"] = await SeriesAsync(
				_context.Messages.AsNoTracking()
					.Where(m => m.SentAt >= fromUtc && m.SentAt <= toUtc)
					.Select(m => m.SentAt)),
			["stories"] = await SeriesAsync(
				_context.Stories.AsNoTracking()
					.Where(s => s.CreatedAt >= fromUtc && s.CreatedAt <= toUtc)
					.Select(s => s.CreatedAt)),
		};

		return new OperatorAiTimeseriesHintsDto
		{
			FromUtc = fromUtc,
			ToUtc = toUtc,
			Bucket = bucket,
			Series = series,
		};
	}

	private static async Task<Dictionary<string, int>> BuildFaceWallTicketStatusCountsAsync(
		ApplicationDbContext ctx,
		CancellationToken cancellationToken)
	{
		var groups = await ctx.FaceWallTickets.AsNoTracking()
			.GroupBy(t => t.Status)
			.Select(g => new { g.Key, Count = g.Count() })
			.ToListAsync(cancellationToken);
		var dict = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var g in groups)
			dict[g.Key.ToString()] = g.Count;
		foreach (var name in Enum.GetNames<FaceWallTicketStatus>())
		{
			if (!dict.ContainsKey(name))
				dict[name] = 0;
		}

		return dict;
	}
}
