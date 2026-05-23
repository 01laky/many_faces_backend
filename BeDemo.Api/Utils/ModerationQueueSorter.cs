using BeDemo.Api.Models.DTOs.Moderation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Utils;

/// <summary>
/// In-memory sort for the moderation queue after filters are applied (queue is not EF-paginated as a single table).
/// <paramref name="sortBy"/> must already pass <see cref="Validation.Moderation.GetModerationQueueQueryValidator"/>.
/// </summary>
public static class ModerationQueueSorter
{
    /// <summary>Orders projected queue rows; default <c>submittedAtUtc</c> descending.</summary>
    public static IEnumerable<ModerationItemDto> ApplySort(
        IEnumerable<ModerationItemDto> items,
        string? sortBy,
        string? sortDir)
    {
        var desc = SortRules.IsDescending(sortDir);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "submittedatutc" => desc
                ? items.OrderByDescending(i => i.SubmittedAtUtc ?? i.CreatedAt)
                : items.OrderBy(i => i.SubmittedAtUtc ?? i.CreatedAt),
            "createdat" => desc ? items.OrderByDescending(i => i.CreatedAt) : items.OrderBy(i => i.CreatedAt),
            "contenttype" => desc ? items.OrderByDescending(i => i.ContentType) : items.OrderBy(i => i.ContentType),
            "contentid" => desc ? items.OrderByDescending(i => i.ContentId) : items.OrderBy(i => i.ContentId),
            "faceid" => desc ? items.OrderByDescending(i => i.FaceId) : items.OrderBy(i => i.FaceId),
            "title" => desc ? items.OrderByDescending(i => i.Title) : items.OrderBy(i => i.Title),
            "approvalstatus" => desc
                ? items.OrderByDescending(i => i.ApprovalStatus)
                : items.OrderBy(i => i.ApprovalStatus),
            "aireviewstatus" => desc
                ? items.OrderByDescending(i => i.AiReviewStatus)
                : items.OrderBy(i => i.AiReviewStatus),
            "aireviewconfidence" => desc
                ? items.OrderByDescending(i => i.AiReviewConfidence ?? 0)
                : items.OrderBy(i => i.AiReviewConfidence ?? 0),
            "risklevel" => desc
                ? items.OrderByDescending(i => i.AiReviewRiskLevel)
                : items.OrderBy(i => i.AiReviewRiskLevel),
            _ => items.OrderByDescending(i => i.SubmittedAtUtc ?? i.CreatedAt),
        };
    }
}
