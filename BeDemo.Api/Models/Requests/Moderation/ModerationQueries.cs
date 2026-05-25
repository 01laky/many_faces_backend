using BeDemo.Api.Models;

namespace BeDemo.Api.Models.Requests.Moderation;

public sealed class GetModerationQueueQuery
{
	public int? ContentId { get; set; }
	public ModeratedContentType? ContentType { get; set; }
	public ContentApprovalStatus? ApprovalStatus { get; set; }
	public AiReviewStatus? AiReviewStatus { get; set; }
	public int? FaceId { get; set; }
	public string? AuthorId { get; set; }
	public AiReviewRiskLevel? RiskLevel { get; set; }
	public int? ModerationVersion { get; set; }
	public string? FlagContains { get; set; }
	public double? MinConfidence { get; set; }
	public double? MaxConfidence { get; set; }
	public DateTime? SubmittedFromUtc { get; set; }
	public DateTime? SubmittedToUtc { get; set; }
	public string? ReviewedByUserId { get; set; }
	public double? MinQueueAgeHours { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 10;
	public string? SortBy { get; set; }
	public string? SortDir { get; set; }
}
