namespace BeDemo.Api.Services;

/// <summary>
/// Documents the security hardening v2 <b>PI-9</b> split between <b>untrusted creator content</b> (moderation pipeline)
/// and <b>trusted operator AI</b> (admin SignalR chat + aggregate public stats).
/// </summary>
/// <remarks>
/// <para>
/// <b>Untrusted path</b> — User-submitted album/blog/reel title, body, and media URL. Any byte sequence may be
/// adversarial (prompt injection, bidi smuggling, homoglyphs). Defenses:
/// <see cref="ContentModerationInputSanitizer"/>,
/// <see cref="ContentModerationPromptInjectionHeuristic"/>,
/// <see cref="ContentModerationHelpers.ValidateRecommendation"/>, invoked only from
/// <see cref="ContentAiReviewService"/> via gRPC <see cref="UntrustedAiRpcName"/>.
/// </para>
/// <para>
/// <b>Trusted operator path</b> — Authenticated platform operators with <c>CanManageAllFaces</c> use
/// SignalR <c>ChatHub</c> (<see cref="TrustedOperatorHubPath"/>) and gRPC <see cref="TrustedOperatorAiRpcNames"/>.
/// Context is operator-typed chat plus optional <see cref="PublicStatsSnapshotJsonSample"/>-shaped aggregate counts
/// from <c>GET /api/Stats/public</c>. That JSON is <b>not</b> scanned by the moderation instruction heuristic and
/// must never be merged into <c>ReviewContent</c> requests. Operator chat messages are not auto-published as public
/// content; moderation policy (auto-approve guards) does not apply to <c>Generate</c> responses.
/// </para>
/// <para>
/// Product docs: monorepo <c>docs/guides/ai-assisted-content-approval.md</c> § “Untrusted creator content vs trusted operator AI”.
/// Operator AI spec: <c>docs/prompts/admin-ai-public-stats-operator-chat-agent-prompt.md</c>.
/// </para>
/// </remarks>
public static class ContentModerationTrustBoundary
{
	/// <summary>gRPC method used exclusively for untrusted album/blog/reel moderation (<c>many_faces_ai</c> classifier).</summary>
	public const string UntrustedAiRpcName = "ReviewContent";

	/// <summary>SignalR hub route for operator and face chat (not the moderation Redis worker).</summary>
	public const string TrustedOperatorHubPath = "/hubs/chat";

	/// <summary>
	/// gRPC entry points for trusted operator AI. Must not call <see cref="ContentModerationInputSanitizer"/> or
	/// apply <see cref="ContentModerationPromptInjectionHeuristic"/> to aggregate stats JSON.
	/// </summary>
	public static readonly IReadOnlyList<string> TrustedOperatorAiRpcNames =
		["Generate", "OperatorStatsChat", "FetchPublicStats"];

	/// <summary>
	/// Representative camelCase JSON shape from <c>PublicStatsSnapshotDto</c> (counts only). Used in tests to prove
	/// aggregate stats are not treated as instruction-like creator submissions.
	/// </summary>
	public const string PublicStatsSnapshotJsonSample =
		"""{"usersCount":1204,"facesCount":89,"pagesCount":12,"friendshipsCount":3401,"pendingFriendRequestsCount":17,"messagesCount":98234,"albumsCount":410,"blogsCount":205,"reelsCount":88,"storiesCount":900,"storyViewsCount":12000,"wallTicketsCount":3,"faceChatRoomsCount":44,"faceChatMessagesCount":5510,"generatedAtUtc":"2026-05-16T12:00:00Z"}""";

	/// <summary>
	/// Returns true when the payload is only trusted operator aggregate stats (no moderation sanitizer required).
	/// </summary>
	public static bool IsTrustedOperatorStatsContext(string? json) =>
		!string.IsNullOrWhiteSpace(json) &&
		json.Contains("\"usersCount\"", StringComparison.Ordinal) &&
		json.Contains("\"generatedAtUtc\"", StringComparison.Ordinal) &&
		!json.Contains("ignore previous", StringComparison.OrdinalIgnoreCase);
}
