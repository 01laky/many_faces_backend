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
	private readonly ApplicationDbContext _context;

	public PlatformStatsQueryService(ApplicationDbContext context) => _context = context;

	/// <inheritdoc />
	public async Task<PublicStatsSnapshotDto> GetPublicSnapshotAsync(CancellationToken cancellationToken = default)
	{
		return new PublicStatsSnapshotDto
		{
			UsersCount = await _context.Users.AsNoTracking().CountAsync(cancellationToken),
			FacesCount = await _context.Faces.AsNoTracking().CountAsync(cancellationToken),
			PagesCount = await _context.Pages.AsNoTracking().CountAsync(cancellationToken),
			FriendshipsCount = await _context.Friendships.AsNoTracking().CountAsync(cancellationToken),
			FriendRequestsPendingCount = await _context.FriendRequests.AsNoTracking()
				.CountAsync(r => r.Status == FriendRequestStatus.Pending, cancellationToken),
			MessagesCount = await _context.Messages.AsNoTracking().CountAsync(cancellationToken),
			AlbumsCount = await _context.Albums.AsNoTracking().CountAsync(cancellationToken),
			BlogsCount = await _context.Blogs.AsNoTracking().CountAsync(cancellationToken),
			ReelsCount = await _context.Reels.AsNoTracking().CountAsync(cancellationToken),
			StoriesCount = await _context.Stories.AsNoTracking().CountAsync(cancellationToken),
			StoryViewsCount = await _context.StoryViews.AsNoTracking().CountAsync(cancellationToken),
			FaceWallTicketsCount = await _context.FaceWallTickets.AsNoTracking().CountAsync(cancellationToken),
			FaceChatRoomsCount = await _context.FaceChatRooms.AsNoTracking().CountAsync(cancellationToken),
			FaceChatRoomMessagesCount = await _context.FaceChatRoomMessages.AsNoTracking().CountAsync(cancellationToken),
			FaceVideoLoungesCount = await _context.FaceVideoLounges.AsNoTracking().CountAsync(cancellationToken),
		};
	}

	/// <inheritdoc />
	public async Task<AdminDashboardSummaryDto> GetOperatorDashboardSummaryAsync(CancellationToken cancellationToken = default)
	{
		return new AdminDashboardSummaryDto
		{
			UsersCount = await _context.Users.AsNoTracking().CountAsync(cancellationToken),
			FriendRequestsCount = await _context.FriendRequests.AsNoTracking()
				.CountAsync(r => r.Status == FriendRequestStatus.Pending, cancellationToken),
			MessagesCount = await _context.Messages.AsNoTracking().CountAsync(cancellationToken),

			FacesCount = await _context.Faces.AsNoTracking().CountAsync(cancellationToken),
			PagesCount = await _context.Pages.AsNoTracking().CountAsync(cancellationToken),
			PageComponentsCount = await _context.PageComponents.AsNoTracking().CountAsync(cancellationToken),
			PageRouteTranslationsCount = await _context.PageRouteTranslations.AsNoTracking().CountAsync(cancellationToken),

			FriendshipsCount = await _context.Friendships.AsNoTracking().CountAsync(cancellationToken),
			FriendRequestsAcceptedCount = await _context.FriendRequests.AsNoTracking()
				.CountAsync(r => r.Status == FriendRequestStatus.Accepted, cancellationToken),
			FriendRequestsRejectedCount = await _context.FriendRequests.AsNoTracking()
				.CountAsync(r => r.Status == FriendRequestStatus.Rejected, cancellationToken),
			UserFollowsCount = await _context.UserFollows.AsNoTracking().CountAsync(cancellationToken),
			UserBlocksCount = await _context.UserBlocks.AsNoTracking().CountAsync(cancellationToken),

			MessagesPendingRequestCount = await _context.Messages.AsNoTracking()
				.CountAsync(m => m.IsMessageRequest && m.MessageRequestStatus == MessageRequestStatus.Pending, cancellationToken),

			NotificationsCount = await _context.Notifications.AsNoTracking().CountAsync(cancellationToken),

			AlbumsCount = await _context.Albums.AsNoTracking().CountAsync(cancellationToken),
			BlogsCount = await _context.Blogs.AsNoTracking().CountAsync(cancellationToken),
			ReelsCount = await _context.Reels.AsNoTracking().CountAsync(cancellationToken),
			StoriesCount = await _context.Stories.AsNoTracking().CountAsync(cancellationToken),
			StoryViewsCount = await _context.StoryViews.AsNoTracking().CountAsync(cancellationToken),

			FaceChatRoomsCount = await _context.FaceChatRooms.AsNoTracking().CountAsync(cancellationToken),
			FaceChatRoomMembersCount = await _context.FaceChatRoomMembers.AsNoTracking().CountAsync(cancellationToken),
			FaceChatRoomMessagesCount = await _context.FaceChatRoomMessages.AsNoTracking().CountAsync(cancellationToken),
			FaceChatRoomJoinRequestsPendingCount = await _context.FaceChatRoomJoinRequests.AsNoTracking()
				.CountAsync(j => j.Status == FaceChatRoomJoinRequestStatus.Pending, cancellationToken),

			FaceVideoLoungesCount = await _context.FaceVideoLounges.AsNoTracking().CountAsync(cancellationToken),
			FaceVideoLoungeMembersCount = await _context.FaceVideoLoungeMembers.AsNoTracking().CountAsync(cancellationToken),
			FaceVideoLoungeLiveSessionsCount = await _context.FaceVideoLoungeSessions.AsNoTracking()
				.CountAsync(s => s.EndedAt == null, cancellationToken),

			FaceWallTicketsCount = await _context.FaceWallTickets.AsNoTracking().CountAsync(cancellationToken),
			FaceWallTicketsByStatus = await BuildFaceWallTicketStatusCountsAsync(cancellationToken),
			FaceWallTicketCommentsCount = await _context.FaceWallTicketComments.AsNoTracking().CountAsync(cancellationToken),
			FaceWallTicketLikesCount = await _context.FaceWallTicketLikes.AsNoTracking().CountAsync(cancellationToken),

			UserFaceProfilesCount = await _context.UserFaceProfiles.AsNoTracking().CountAsync(cancellationToken),
			UserFaceProfileLikesCount = await _context.UserFaceProfileLikes.AsNoTracking().CountAsync(cancellationToken),
			UserFaceProfileCommentsCount = await _context.UserFaceProfileComments.AsNoTracking().CountAsync(cancellationToken),
			UserFaceProfileReviewsCount = await _context.UserFaceProfileReviews.AsNoTracking().CountAsync(cancellationToken),

			AlbumCommentsCount = await _context.AlbumComments.AsNoTracking().CountAsync(cancellationToken),
			BlogCommentsCount = await _context.BlogComments.AsNoTracking().CountAsync(cancellationToken),
			ReelCommentsCount = await _context.ReelComments.AsNoTracking().CountAsync(cancellationToken),
			StoryCommentsCount = await _context.StoryComments.AsNoTracking().CountAsync(cancellationToken),
			AlbumLikesCount = await _context.AlbumLikes.AsNoTracking().CountAsync(cancellationToken),
			BlogLikesCount = await _context.BlogLikes.AsNoTracking().CountAsync(cancellationToken),
			ReelLikesCount = await _context.ReelLikes.AsNoTracking().CountAsync(cancellationToken),
			StoryLikesCount = await _context.StoryLikes.AsNoTracking().CountAsync(cancellationToken),

			AiReviewJobsCount = await _context.AiReviewJobs.AsNoTracking().CountAsync(cancellationToken),
			ContentModerationEventsCount = await _context.ContentModerationEvents.AsNoTracking().CountAsync(cancellationToken),

			OAuthClientsCount = await _context.OAuthClients.AsNoTracking().CountAsync(cancellationToken),
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

	private async Task<Dictionary<string, int>> BuildFaceWallTicketStatusCountsAsync(CancellationToken cancellationToken)
	{
		var groups = await _context.FaceWallTickets.AsNoTracking()
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
