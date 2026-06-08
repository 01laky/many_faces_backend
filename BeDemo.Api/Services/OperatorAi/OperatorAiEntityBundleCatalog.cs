using BeDemo.Api.Models.DTOs.OperatorAi;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>Stable v2 catalog: one bundle per EF entity, indices 0–60.</summary>
public static class OperatorAiEntityBundleCatalog
{
	public const int CatalogVersion = 2;
	public const int BundleCount = 61;

	private static readonly OperatorAiBundleCatalogEntryDto[] Entries =
	[
		Entry(0, "entity.users", "ApplicationUser", "Users", "Platform login accounts — total count, optional 7-day signup daily trend."),
		Entry(1, "entity.userProfiles", "UserProfile", "UserProfiles", "Extended profile rows (1:1 with users) — total count."),
		Entry(2, "entity.userRoles", "UserRole", "UserRoles", "Global role definitions — row count (reference)."),
		Entry(3, "entity.userFaceRoles", "UserFaceRole", "UserFaceRoles", "Face-scoped role assignments — total count."),
		Entry(4, "entity.registrationInvites", "RegistrationInvite", "RegistrationInvites", "Pending email-code signups — counts by lifecycle."),
		Entry(5, "entity.userPushDevices", "UserPushDevice", "UserPushDevices", "Registered mobile push tokens — total count."),
		Entry(6, "entity.friendships", "Friendship", "Friendships", "Accepted mutual friendships — total count."),
		Entry(7, "entity.friendRequests", "FriendRequest", "FriendRequests", "Friend requests — total + pending/accepted/rejected breakdown."),
		Entry(8, "entity.userFollows", "UserFollow", "UserFollows", "Directed follow edges — total count."),
		Entry(9, "entity.userBlocks", "UserBlock", "UserBlocks", "User block edges — total count."),
		Entry(10, "entity.messages", "Message", "Messages", "Direct messages — total + pending message-request count."),
		Entry(11, "entity.notifications", "Notification", "Notifications", "In-app notifications — total + count grouped by Type."),
		Entry(12, "entity.faces", "Face", "Faces", "Tenant faces — total count."),
		Entry(13, "entity.pages", "Page", "Pages", "CMS pages — total count."),
		Entry(14, "entity.pageTypes", "PageType", "PageTypes", "Page type reference rows — count."),
		Entry(15, "entity.pageComponents", "PageComponent", "PageComponents", "Grid components on pages — total count."),
		Entry(16, "entity.pageRouteTranslations", "PageRouteTranslation", "PageRouteTranslations", "Localized route slugs — total count."),
		Entry(17, "entity.componentTypes", "ComponentType", "ComponentTypes", "Component type reference — count."),
		Entry(18, "entity.displayModes", "DisplayMode", "DisplayModes", "Display mode reference — count."),
		Entry(19, "entity.userFaceProfiles", "UserFaceProfile", "UserFaceProfiles", "Profiles inside faces — total count."),
		Entry(20, "entity.userFaceProfileComments", "UserFaceProfileComment", "UserFaceProfileComments", "Comments on face profiles — total + 7-day trend."),
		Entry(21, "entity.userFaceProfileLikes", "UserFaceProfileLike", "UserFaceProfileLikes", "Likes on face profiles — total count."),
		Entry(22, "entity.userFaceProfileReviews", "UserFaceProfileReview", "UserFaceProfileReviews", "Reviews on face profiles — total count."),
		Entry(23, "entity.albums", "Album", "Albums", "Photo albums — total + approval/AI review status breakdown."),
		Entry(24, "entity.albumFaces", "AlbumFace", "AlbumFaces", "Album-to-face links — total count."),
		Entry(25, "entity.albumMedia", "AlbumMedia", "AlbumMedia", "Media items in albums — total count."),
		Entry(26, "entity.albumComments", "AlbumComment", "AlbumComments", "Album comments — total + 7-day trend."),
		Entry(27, "entity.albumLikes", "AlbumLike", "AlbumLikes", "Album likes — total count."),
		Entry(28, "entity.blogs", "Blog", "Blogs", "Blog posts — total + approval/AI review status breakdown."),
		Entry(29, "entity.blogImages", "BlogImage", "BlogImages", "Images attached to blogs — total count."),
		Entry(30, "entity.blogComments", "BlogComment", "BlogComments", "Blog comments — total + 7-day trend."),
		Entry(31, "entity.blogLikes", "BlogLike", "BlogLikes", "Blog likes — total count."),
		Entry(32, "entity.reels", "Reel", "Reels", "Short video reels — total + approval/AI review status breakdown."),
		Entry(33, "entity.reelFaces", "ReelFace", "ReelFaces", "Reel-to-face links — total count."),
		Entry(34, "entity.reelComments", "ReelComment", "ReelComments", "Reel comments — total + 7-day trend."),
		Entry(35, "entity.reelLikes", "ReelLike", "ReelLikes", "Reel likes — total count."),
		Entry(36, "entity.stories", "Story", "Stories", "Stories — total + state breakdown."),
		Entry(37, "entity.storyFaces", "StoryFace", "StoryFaces", "Story-to-face links — total count."),
		Entry(38, "entity.storyImages", "StoryImage", "StoryImages", "Story slide images — total count."),
		Entry(39, "entity.storyComments", "StoryComment", "StoryComments", "Story comments — total + 7-day trend."),
		Entry(40, "entity.storyLikes", "StoryLike", "StoryLikes", "Story likes — total count."),
		Entry(41, "entity.storyViews", "StoryView", "StoryViews", "Story view events — total + 7-day trend."),
		Entry(42, "entity.faceChatRooms", "FaceChatRoom", "FaceChatRooms", "Face-scoped chat rooms — total count."),
		Entry(43, "entity.faceChatRoomMembers", "FaceChatRoomMember", "FaceChatRoomMembers", "Chat room memberships — total count."),
		Entry(44, "entity.faceChatRoomMessages", "FaceChatRoomMessage", "FaceChatRoomMessages", "Messages in face chat rooms — total + 7-day trend."),
		Entry(45, "entity.faceChatRoomJoinRequests", "FaceChatRoomJoinRequest", "FaceChatRoomJoinRequests", "Join requests — total + pending/approved/denied."),
		Entry(46, "entity.faceVideoLounges", "FaceVideoLounge", "FaceVideoLounges", "Video lounge rooms — total count."),
		Entry(47, "entity.faceVideoLoungeMembers", "FaceVideoLoungeMember", "FaceVideoLoungesMembers", "Lounge memberships — total count."),
		Entry(48, "entity.faceVideoLoungeJoinRequests", "FaceVideoLoungeJoinRequest", "FaceVideoLoungeJoinRequests", "Lounge join requests — total + status breakdown."),
		Entry(49, "entity.faceVideoLoungeSessions", "FaceVideoLoungeSession", "FaceVideoLoungesSessions", "Live sessions — total + currently live."),
		Entry(50, "entity.faceVideoLoungeSessionParticipants", "FaceVideoLoungeSessionParticipant", "FaceVideoLoungeSessionParticipants", "Session participant rows — total count."),
		Entry(51, "entity.faceWallTickets", "FaceWallTicket", "FaceWallTickets", "Wall tickets — total + Active/Approved/Denied breakdown."),
		Entry(52, "entity.faceWallTicketComments", "FaceWallTicketComment", "FaceWallTicketComments", "Wall ticket comments — total + 7-day trend."),
		Entry(53, "entity.faceWallTicketLikes", "FaceWallTicketLike", "FaceWallTicketLikes", "Wall ticket likes — total count."),
		Entry(54, "entity.aiReviewJobs", "AiReviewJob", "AiReviewJobs", "AI moderation jobs — total + status breakdown."),
		Entry(55, "entity.contentModerationEvents", "ContentModerationEvent", "ContentModerationEvents", "Moderation audit events — total + 7-day trend."),
		Entry(56, "entity.userFaceModerations", "UserFaceModeration", "UserFaceModerations", "Operator face bans — active vs lifted counts."),
		Entry(57, "entity.oauthClients", "OAuthClient", "OAuthClients", "OAuth confidential clients — count (no secrets)."),
		Entry(58, "entity.oauthRefreshTokens", "OAuthRefreshToken", "OAuthRefreshTokens", "Refresh tokens — active vs revoked/expired counts."),
		Entry(59, "entity.operatorAiConversations", "OperatorAiConversation", "OperatorAiConversations", "Operator support AI threads — total count."),
		Entry(60, "entity.operatorAiMessages", "OperatorAiMessage", "OperatorAiMessages", "Messages in operator AI threads — total + 7-day trend."),
	];

	public static IReadOnlyList<OperatorAiBundleCatalogEntryDto> ListMetadata() => Entries;

	public static OperatorAiBundleCatalogEntryDto GetByIndex(int index)
	{
		if (index < 0 || index >= Entries.Length)
			throw new ArgumentOutOfRangeException(nameof(index));
		return Entries[index];
	}

	public static OperatorAiBundleCatalogDto ToPlannerCatalogDto() =>
		new()
		{
			CatalogVersion = CatalogVersion,
			BundleCount = BundleCount,
			Bundles = Entries,
		};

	private static OperatorAiBundleCatalogEntryDto Entry(
		int index,
		string id,
		string entity,
		string dbSet,
		string description)
	{
		// RAG retrieval (§7.1): merge the authored synonyms + sample questions (single source of truth,
		// OperatorAiKnowledgeDescriptors) onto the catalog metadata so the indexer can build content_text.
		var (synonyms, sampleQuestions) =
			OperatorAiKnowledgeDescriptors.ByIndex.TryGetValue(index, out var d)
				? (d.Synonyms, d.SampleQuestions)
				: (Array.Empty<string>(), Array.Empty<string>());

		return new()
		{
			Index = index,
			Id = id,
			EntityName = entity,
			Description = description,
			EndpointKey = id,
			Synonyms = synonyms,
			SampleQuestions = sampleQuestions,
		};
	}
}
