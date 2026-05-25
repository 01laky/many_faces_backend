namespace BeDemo.Api.Models;

public class Reel
{
	public int Id { get; set; }
	public string CreatorId { get; set; } = null!;
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	/// <summary>URL of the single video (e.g. CDN or uploaded file path).</summary>
	public string VideoUrl { get; set; } = string.Empty;
	public ContentApprovalStatus ApprovalStatus { get; set; } = ContentApprovalStatus.Approved;
	public AiReviewStatus AiReviewStatus { get; set; } = AiReviewStatus.NotQueued;
	public AiReviewDecision AiReviewDecision { get; set; } = AiReviewDecision.None;
	public double? AiReviewConfidence { get; set; }
	public AiReviewRiskLevel AiReviewRiskLevel { get; set; } = AiReviewRiskLevel.Unknown;
	public string? AiReviewFlagsJson { get; set; }
	public string? AiReviewReason { get; set; }
	public string? AiReviewUserMessage { get; set; }
	public string? AiReviewModelVersion { get; set; }
	public string? AiReviewTraceId { get; set; }
	public DateTime? SubmittedAtUtc { get; set; }
	public DateTime? AiReviewedAtUtc { get; set; }
	public DateTime? HumanReviewedAtUtc { get; set; }
	public string? HumanReviewedByUserId { get; set; }
	public string? HumanDecisionReason { get; set; }
	public DateTime? RemovedAtUtc { get; set; }
	public string? RemovedByUserId { get; set; }
	public string? RemovalReason { get; set; }
	public int ModerationVersion { get; set; } = 1;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }

	public ApplicationUser Creator { get; set; } = null!;
	public ICollection<ReelFace> ReelFaces { get; set; } = new List<ReelFace>();
	public ICollection<ReelComment> Comments { get; set; } = new List<ReelComment>();
	public ICollection<ReelLike> Likes { get; set; } = new List<ReelLike>();
}
