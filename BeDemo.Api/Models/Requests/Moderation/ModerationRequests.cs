using BeDemo.Api.Models;

namespace BeDemo.Api.Models.Requests.Moderation;

public sealed record ModerationDecisionDto(string? Reason, string? UserMessage);

public enum BulkModerationAction
{
	Approve = 1,
	Reject = 2,
	Remove = 3,
	RequeueAiReview = 4,
}

public sealed record BulkModerationRequest(
	BulkModerationAction Action,
	List<BulkModerationItemDto> Items,
	string? Reason,
	string? UserMessage);

public sealed record BulkModerationItemDto(ModeratedContentType ContentType, int ContentId);
