namespace BeDemo.Api.Services;

/// <summary>
/// Configuration for <b>untrusted</b> creator-content defenses on the path to gRPC
/// <see cref="ContentModerationTrustBoundary.UntrustedAiRpcName"/> only.
/// </summary>
/// <remarks>
/// Security hardening v2 <b>PI-9</b>: does not apply to trusted operator AI
/// (<see cref="ContentModerationTrustBoundary.TrustedOperatorAiRpcNames"/> / SignalR
/// <see cref="ContentModerationTrustBoundary.TrustedOperatorHubPath"/>). Operator chat and
/// <c>PublicStatsSnapshotDto</c> JSON use separate ACL, rate limits, and prompt templates.
/// </remarks>
public sealed class ContentModerationSecurityOptions
{
	public const string SectionName = "ContentModeration";

	/// <summary>
	/// When true, title/body (as stored for the submission) are scanned for instruction-like patterns;
	/// any <see cref="AiReviewDecision.Approve"/> from the AI is downgraded to human review after validation.
	/// </summary>
	public bool InstructionHeuristicEnabled { get; set; } = true;
}
