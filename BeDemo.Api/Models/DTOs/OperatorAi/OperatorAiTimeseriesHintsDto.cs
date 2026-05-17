namespace BeDemo.Api.Models.DTOs.OperatorAi;

/// <summary>
/// Compact daily buckets for common metrics (operator AI context only).
/// </summary>
public sealed class OperatorAiTimeseriesHintsDto
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public string Bucket { get; init; } = "day";

    /// <summary>Metric name → daily counts (e.g. users, messages, stories).</summary>
    public Dictionary<string, IReadOnlyList<OperatorAiTimeseriesBucketDto>> Series { get; init; } = new();
}

public sealed class OperatorAiTimeseriesBucketDto
{
    public DateTime PeriodStartUtc { get; init; }
    public int Count { get; init; }
}
