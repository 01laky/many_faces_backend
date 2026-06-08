namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// Single source of truth (§17.6) for the per-bundle RAG descriptor metadata: <c>synonyms</c> and
/// <c>sample_questions</c> authored for ALL 61 stat bundles (catalog indices 0..60).
///
/// <para>What/why:</para>
/// The RAG index embeds each bundle's <em>question-space</em> — what it can answer — not its counts.
/// The embedded <c>content_text</c> per bundle is
/// <c>description + "\n" + synonyms.join(", ") + "\n" + sample_questions.join(" ")</c> (§7.1). Synonyms
/// (entity aliases / alt phrasings) and sample questions materially improve routing recall over the tiny
/// 61-item descriptor set, so they are authored here and merged into the catalog metadata.
///
/// <para>Coverage lint (§17.6, RT-18):</para>
/// A build-time unit test asserts every index 0..60 has non-empty synonyms AND sample_questions, and that
/// all knowledge ids are unique. Keeping authoring in this one file makes that lint deterministic.
/// </summary>
internal static class OperatorAiKnowledgeDescriptors
{
	/// <summary>Authored synonyms + sample questions keyed by catalog index 0..60.</summary>
	public static readonly IReadOnlyDictionary<int, (string[] Synonyms, string[] SampleQuestions)> ByIndex =
		new Dictionary<int, (string[], string[])>
		{
			[0] = (
				["users", "accounts", "members", "registered users", "logins", "people", "user count"],
				["how many users are registered?", "how many people signed up this week?", "what is the daily signup trend?", "total number of accounts"]),
			[1] = (
				["profiles", "user profiles", "bio", "profile rows", "account details"],
				["how many user profiles exist?", "count of extended profile rows", "how many users completed their profile?"]),
			[2] = (
				["roles", "global roles", "rbac roles", "role definitions", "permissions"],
				["how many global roles are defined?", "list the role definitions count", "how many rbac roles exist?"]),
			[3] = (
				["face roles", "face-scoped roles", "tenant role assignments", "user face roles"],
				["how many face-scoped role assignments are there?", "count of per-face role grants"]),
			[4] = (
				["invites", "registration invites", "email codes", "signup invites", "pending signups"],
				["how many registration invites are pending?", "how many email-code signups are outstanding?", "invite lifecycle breakdown"]),
			[5] = (
				["push devices", "push tokens", "mobile devices", "fcm tokens", "registered devices"],
				["how many push devices are registered?", "count of mobile push tokens", "how many devices can receive notifications?"]),
			[6] = (
				["friendships", "friends", "mutual friends", "accepted friends", "friend connections"],
				["how many friendships are there?", "count of accepted mutual friends", "friendship growth this week"]),
			[7] = (
				["friend requests", "pending friend requests", "friend invites"],
				["how many friend requests are pending?", "friend request accepted vs rejected breakdown", "count of outstanding friend invites"]),
			[8] = (
				["follows", "followers", "following", "follow edges", "user follows"],
				["how many follow relationships exist?", "count of follow edges", "how many users are following others?"]),
			[9] = (
				["blocks", "blocked users", "user blocks", "block edges"],
				["how many user blocks are there?", "count of blocked user relationships"]),
			[10] = (
				["messages", "direct messages", "dms", "private messages", "message requests"],
				["how many direct messages were sent?", "how many message requests are pending?", "total DM count"]),
			[11] = (
				["notifications", "alerts", "in-app notifications", "push notifications"],
				["how many notifications were sent?", "notification count grouped by type", "notifications breakdown by type"]),
			[12] = (
				["faces", "tenants", "tenant faces", "communities", "spaces"],
				["how many faces (tenants) are there?", "total number of communities", "count of tenant faces"]),
			[13] = (
				["pages", "cms pages", "content pages", "site pages"],
				["how many CMS pages exist?", "total page count", "how many content pages are published?"]),
			[14] = (
				["page types", "page type reference", "page categories"],
				["how many page types are defined?", "count of page type reference rows"]),
			[15] = (
				["page components", "grid components", "components", "page widgets"],
				["how many page components are there?", "count of grid components on pages"]),
			[16] = (
				["route translations", "page route translations", "localized routes", "slugs"],
				["how many localized route slugs exist?", "count of page route translations"]),
			[17] = (
				["component types", "component type reference", "widget types"],
				["how many component types are defined?", "count of component type reference rows"]),
			[18] = (
				["display modes", "display mode reference", "layout modes"],
				["how many display modes are defined?", "count of display mode reference rows"]),
			[19] = (
				["face profiles", "user face profiles", "profiles inside faces", "tenant profiles"],
				["how many profiles exist inside faces?", "count of user face profiles", "how many tenant-scoped profiles are there?"]),
			[20] = (
				["profile comments", "face profile comments", "comments on profiles"],
				["how many comments are on face profiles?", "profile comment trend this week", "count of face profile comments"]),
			[21] = (
				["profile likes", "face profile likes", "likes on profiles"],
				["how many likes are on face profiles?", "count of face profile likes"]),
			[22] = (
				["profile reviews", "face profile reviews", "ratings", "reviews on profiles"],
				["how many reviews are on face profiles?", "count of face profile reviews"]),
			[23] = (
				["albums", "photo albums", "galleries", "picture sets", "photo collections"],
				["how many albums are pending approval?", "album approval status breakdown", "how many albums failed AI review?", "album upload trend"]),
			[24] = (
				["album faces", "album-to-face links", "album tenant links"],
				["how many album-to-face links exist?", "count of album face links"]),
			[25] = (
				["album media", "album photos", "media items", "album images", "uploads"],
				["how many media items are in albums?", "count of album media uploads"]),
			[26] = (
				["album comments", "comments on albums", "photo comments"],
				["how many album comments are there?", "album comment trend this week"]),
			[27] = (
				["album likes", "likes on albums", "photo likes"],
				["how many album likes are there?", "count of likes on albums"]),
			[28] = (
				["blogs", "blog posts", "articles", "posts", "stories written"],
				["how many blogs are pending approval?", "blog approval status breakdown", "how many blog posts failed AI review?", "blog publishing trend"]),
			[29] = (
				["blog images", "images on blogs", "blog photos", "article images"],
				["how many images are attached to blogs?", "count of blog images"]),
			[30] = (
				["blog comments", "comments on blogs", "article comments"],
				["how many blog comments are there?", "blog comment trend this week"]),
			[31] = (
				["blog likes", "likes on blogs", "article likes"],
				["how many blog likes are there?", "count of likes on blogs"]),
			[32] = (
				["reels", "short videos", "video reels", "clips", "short-form video"],
				["how many reels are pending approval?", "reel approval status breakdown", "how many reels failed AI review?", "reel upload trend"]),
			[33] = (
				["reel faces", "reel-to-face links", "reel tenant links"],
				["how many reel-to-face links exist?", "count of reel face links"]),
			[34] = (
				["reel comments", "comments on reels", "video comments"],
				["how many reel comments are there?", "reel comment trend this week"]),
			[35] = (
				["reel likes", "likes on reels", "video likes"],
				["how many reel likes are there?", "count of likes on reels"]),
			[36] = (
				["stories", "ephemeral stories", "story posts", "24h stories"],
				["how many stories were posted?", "story state breakdown", "active vs expired stories"]),
			[37] = (
				["story faces", "story-to-face links", "story tenant links"],
				["how many story-to-face links exist?", "count of story face links"]),
			[38] = (
				["story images", "story slides", "story photos", "slide images"],
				["how many story slide images are there?", "count of story images"]),
			[39] = (
				["story comments", "comments on stories", "story replies"],
				["how many story comments are there?", "story comment trend this week"]),
			[40] = (
				["story likes", "likes on stories", "story reactions"],
				["how many story likes are there?", "count of likes on stories"]),
			[41] = (
				["story views", "story impressions", "views on stories", "story view events"],
				["how many story views were recorded?", "story view trend this week", "count of story view events"]),
			[42] = (
				["chat rooms", "face chat rooms", "group chats", "rooms"],
				["how many face chat rooms exist?", "total chat room count", "how many group chats are there?"]),
			[43] = (
				["chat room members", "room memberships", "chat members"],
				["how many chat room memberships are there?", "count of chat room members"]),
			[44] = (
				["chat room messages", "room messages", "group chat messages"],
				["how many messages were sent in face chat rooms?", "chat room message trend this week", "group chat message volume"]),
			[45] = (
				["join requests", "chat room join requests", "room access requests"],
				["how many chat room join requests are pending?", "join request approved vs denied breakdown"]),
			[46] = (
				["video lounges", "lounges", "video rooms", "face video lounges"],
				["how many video lounges exist?", "total video lounge count"]),
			[47] = (
				["lounge members", "video lounge members", "lounge memberships"],
				["how many video lounge memberships are there?", "count of lounge members"]),
			[48] = (
				["lounge join requests", "video lounge join requests", "lounge access requests"],
				["how many video lounge join requests are pending?", "lounge join request status breakdown"]),
			[49] = (
				["lounge sessions", "video lounge sessions", "live sessions", "live calls"],
				["how many video lounge sessions ran?", "how many lounges are currently live?", "live session count"]),
			[50] = (
				["session participants", "lounge session participants", "call participants"],
				["how many participants joined lounge sessions?", "count of session participant rows"]),
			[51] = (
				["wall tickets", "tickets", "wall posts", "face wall tickets", "complaints"],
				["how many wall tickets are active?", "wall ticket approved vs denied breakdown", "how many tickets are pending?"]),
			[52] = (
				["wall ticket comments", "ticket comments", "comments on wall tickets"],
				["how many wall ticket comments are there?", "wall ticket comment trend this week"]),
			[53] = (
				["wall ticket likes", "ticket likes", "likes on wall tickets"],
				["how many wall ticket likes are there?", "count of likes on wall tickets"]),
			[54] = (
				["ai review jobs", "moderation jobs", "ai moderation", "content review jobs", "auto-moderation"],
				["how many AI moderation jobs ran?", "AI review job status breakdown", "how many moderation jobs are pending or failed?"]),
			[55] = (
				["moderation events", "content moderation events", "moderation audit", "moderation log"],
				["how many content moderation events were recorded?", "moderation event trend this week", "moderation audit volume"]),
			[56] = (
				["face moderations", "user face moderations", "bans", "operator bans", "user bans"],
				["how many users are banned from faces?", "active vs lifted face bans", "count of operator face moderations"]),
			[57] = (
				["oauth clients", "oauth confidential clients", "api clients", "oauth apps"],
				["how many OAuth clients are registered?", "count of confidential OAuth clients"]),
			[58] = (
				["refresh tokens", "oauth refresh tokens", "sessions", "active tokens"],
				["how many refresh tokens are active?", "active vs revoked refresh tokens", "count of expired tokens"]),
			[59] = (
				["operator ai conversations", "ai threads", "support threads", "operator chat threads"],
				["how many operator AI conversations exist?", "total operator AI thread count"]),
			[60] = (
				["operator ai messages", "ai thread messages", "support messages", "operator chat messages"],
				["how many messages are in operator AI threads?", "operator AI message trend this week", "operator AI message volume"]),
		};
}
