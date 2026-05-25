namespace BeDemo.Api.Models;

public class Blog
{
	public int Id { get; set; }
	public string CreatorId { get; set; } = null!;
	public int FaceId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
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
	public Face Face { get; set; } = null!;
	public ICollection<BlogImage> Images { get; set; } = new List<BlogImage>();
	public ICollection<BlogComment> Comments { get; set; } = new List<BlogComment>();
	public ICollection<BlogLike> Likes { get; set; } = new List<BlogLike>();
}
