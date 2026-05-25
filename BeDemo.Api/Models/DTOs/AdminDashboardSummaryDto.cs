using BeDemo.Api.Models;

namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// Consolidated read-model for the admin SPA home dashboard (<c>GET /api/Stats</c>).
/// All counts are non-negative platform totals; callers must be authorized as platform operators
/// (admin face HTTP scope + global Admin/SuperAdmin JWT — same bar as <c>UsersController</c>).
/// </summary>
/// <remarks>
/// Field names are camelCase in JSON (ASP.NET default). Legacy clients may only read the first three fields;
/// newer clients consume the full document. Do not rename <c>friendRequestsCount</c> — it remains the
/// <strong>pending</strong> friend-request count for backward compatibility with older admin bundles.
/// See <see cref="FriendRequestStatus.Pending"/>.
/// </remarks>
public sealed class AdminDashboardSummaryDto
{
	// --- Legacy-compatible (unchanged semantics) ---
	public int UsersCount { get; init; }
	/// <summary>Pending <see cref="FriendRequestStatus.Pending"/> rows only (legacy name).</summary>
	public int FriendRequestsCount { get; init; }
	public int MessagesCount { get; init; }

	// --- Core directory / CMS ---
	public int FacesCount { get; init; }
	public int PagesCount { get; init; }
	public int PageComponentsCount { get; init; }
	public int PageRouteTranslationsCount { get; init; }

	// --- Social graph ---
	public int FriendshipsCount { get; init; }
	public int FriendRequestsAcceptedCount { get; init; }
	public int FriendRequestsRejectedCount { get; init; }
	public int UserFollowsCount { get; init; }
	public int UserBlocksCount { get; init; }

	// --- Direct messaging ---
	/// <summary>Rows where <see cref="Message.IsMessageRequest"/> is true and status is <see cref="MessageRequestStatus.Pending"/>.</summary>
	public int MessagesPendingRequestCount { get; init; }

	// --- Notifications (no read/unread model on entity — total rows only) ---
	public int NotificationsCount { get; init; }

	// --- User-generated content (totals) ---
	public int AlbumsCount { get; init; }
	public int BlogsCount { get; init; }
	public int ReelsCount { get; init; }
	public int StoriesCount { get; init; }
	public int StoryViewsCount { get; init; }

	// --- Face-scoped chat ---
	public int FaceChatRoomsCount { get; init; }
	public int FaceChatRoomMembersCount { get; init; }
	public int FaceChatRoomMessagesCount { get; init; }
	public int FaceChatRoomJoinRequestsPendingCount { get; init; }

	// --- Face-scoped video lounges (stats only — no AI moderation) ---
	public int FaceVideoLoungesCount { get; init; }
	public int FaceVideoLoungeMembersCount { get; init; }
	public int FaceVideoLoungeLiveSessionsCount { get; init; }

	// --- Wall ---
	public int FaceWallTicketsCount { get; init; }
	/// <summary>Counts keyed by enum name: Active, Approved, Denied.</summary>
	public Dictionary<string, int> FaceWallTicketsByStatus { get; init; } = new();
	public int FaceWallTicketCommentsCount { get; init; }
	public int FaceWallTicketLikesCount { get; init; }

	// --- Profiles inside faces ---
	public int UserFaceProfilesCount { get; init; }
	public int UserFaceProfileLikesCount { get; init; }
	public int UserFaceProfileCommentsCount { get; init; }
	public int UserFaceProfileReviewsCount { get; init; }

	// --- Comments / likes on feed items ---
	public int AlbumCommentsCount { get; init; }
	public int BlogCommentsCount { get; init; }
	public int ReelCommentsCount { get; init; }
	public int StoryCommentsCount { get; init; }
	public int AlbumLikesCount { get; init; }
	public int BlogLikesCount { get; init; }
	public int ReelLikesCount { get; init; }
	public int StoryLikesCount { get; init; }

	// --- Moderation pipeline (raw table sizes — detailed queue metrics stay on ContentModeration API) ---
	public int AiReviewJobsCount { get; init; }
	public int ContentModerationEventsCount { get; init; }

	// --- OAuth clients (public metadata count; excludes secret material) ---
	public int OAuthClientsCount { get; init; }
}
