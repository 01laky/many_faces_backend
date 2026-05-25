namespace BeDemo.Api.Models;

public enum AlbumTypeEnum
{
	Public = 1,
	Private = 2,
	Paid = 3
}

public enum MediaTypeEnum
{
	Image = 1,
	Video = 2
}

public class Album
{
	public int Id { get; set; }
	public string CreatorId { get; set; } = null!;
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AlbumTypeEnum AlbumType { get; set; } = AlbumTypeEnum.Public;
	public MediaTypeEnum MediaType { get; set; } = MediaTypeEnum.Image;
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
	public ICollection<AlbumFace> AlbumFaces { get; set; } = new List<AlbumFace>();
	public ICollection<AlbumComment> Comments { get; set; } = new List<AlbumComment>();
	public ICollection<AlbumLike> Likes { get; set; } = new List<AlbumLike>();
	public ICollection<AlbumMedia> MediaItems { get; set; } = new List<AlbumMedia>();
}
