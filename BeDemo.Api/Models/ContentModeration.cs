namespace BeDemo.Api.Models;

/// <summary>
/// Enums and persistence models for AI-assisted user content moderation (albums, blogs, reels).
/// Approval is always human-gated for non-owner publication; AI fields hold advisory recommendations only.
/// </summary>
public enum ModeratedContentType
{
	Album = 1,
	Blog = 2,
	Reel = 3,
}

public enum ContentApprovalStatus
{
	PendingApproval = 1,
	Approved = 2,
	Rejected = 3,
	Removed = 4,
}

public enum AiReviewStatus
{
	NotQueued = 1,
	Queued = 2,
	InProgress = 3,
	RecommendedApprove = 4,
	RecommendedReject = 5,
	NeedsHumanReview = 6,
	Failed = 7,
}

public enum AiReviewDecision
{
	None = 0,
	Approve = 1,
	Reject = 2,
	NeedsHumanReview = 3,
}

public enum AiReviewRiskLevel
{
	Unknown = 0,
	Low = 1,
	Medium = 2,
	High = 3,
}

public enum AiReviewJobStatus
{
	Queued = 1,
	Processing = 2,
	Completed = 3,
	RetryScheduled = 4,
	Failed = 5,
	NeedsHumanReview = 6,
}

public enum ModerationActorType
{
	User = 1,
	AI = 2,
	Admin = 3,
	SuperAdmin = 4,
	System = 5,
	/// <summary>Automated retention worker redacting internal AI fields after policy delay.</summary>
	Retention = 6,
}

public sealed class AiReviewJob
{
	public int Id { get; set; }
	public ModeratedContentType ContentType { get; set; }
	public int ContentId { get; set; }
	public int FaceId { get; set; }
	public string CreatedByUserId { get; set; } = string.Empty;
	public int Priority { get; set; }
	public AiReviewJobStatus Status { get; set; } = AiReviewJobStatus.Queued;
	public int Attempts { get; set; }
	public int MaxAttempts { get; set; } = 3;
	public int ModerationVersion { get; set; } = 1;
	public DateTime? NextAttemptAtUtc { get; set; }
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public DateTime? StartedAtUtc { get; set; }
	public DateTime? CompletedAtUtc { get; set; }
	public string? LastError { get; set; }
}

public sealed class ContentModerationEvent
{
	public int Id { get; set; }
	public ModeratedContentType ContentType { get; set; }
	public int ContentId { get; set; }
	public int FaceId { get; set; }
	public ContentApprovalStatus? OldApprovalStatus { get; set; }
	public ContentApprovalStatus? NewApprovalStatus { get; set; }
	public AiReviewStatus? OldAiReviewStatus { get; set; }
	public AiReviewStatus? NewAiReviewStatus { get; set; }
	public ModerationActorType ActorType { get; set; }
	public string? ActorUserId { get; set; }
	public string? Reason { get; set; }
	public string? UserMessage { get; set; }
	public string? AiTraceId { get; set; }
	public string? AiModelVersion { get; set; }
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
