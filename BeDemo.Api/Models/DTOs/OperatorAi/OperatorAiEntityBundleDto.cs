using System.Text.Json.Serialization;

namespace BeDemo.Api.Models.DTOs.OperatorAi;

/// <summary>Aggregate stats payload for one EF entity bundle (live map-reduce v1).</summary>
public sealed class OperatorAiEntityBundleDto
{
    public required string BundleId { get; init; }
    public required string Entity { get; init; }
    public required int Index { get; init; }
    public required DateTime SnapshotUtc { get; init; }
    public required int TotalCount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? ByStatus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? ByAiReviewStatus { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? ByType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<OperatorAiEntityBundleTimeseriesBucketDto>? TimeseriesLast7Days { get; init; }
}

public sealed class OperatorAiEntityBundleTimeseriesBucketDto
{
    public DateTime PeriodStartUtc { get; init; }
    public int Count { get; init; }
}

public sealed class OperatorAiBundleCatalogEntryDto
{
    public required int Index { get; init; }
    public required string Id { get; init; }
    public required string EntityName { get; init; }
    public required string Description { get; init; }
    public required string EndpointKey { get; init; }
}

public sealed class OperatorAiBundleCatalogDto
{
    public int CatalogVersion { get; init; } = 2;
    public int BundleCount { get; init; } = 61;
    public required IReadOnlyList<OperatorAiBundleCatalogEntryDto> Bundles { get; init; }
}

public sealed class OperatorAiLivePlannerResultDto
{
    public required IReadOnlyList<int> Indices { get; init; }
    public string? Reason { get; init; }
}
