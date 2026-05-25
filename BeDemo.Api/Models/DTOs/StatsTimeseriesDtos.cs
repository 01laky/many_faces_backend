namespace BeDemo.Api.Models.DTOs;

/// <summary>
/// One histogram bucket for <c>GET /api/Stats/timeseries</c> (UTC period start + row count in that period).
/// </summary>
public sealed class StatsTimeseriesBucketDto
{
	/// <summary>Bucket start instant in UTC (midnight for day buckets; Monday 00:00 UTC for ISO week buckets).</summary>
	public DateTime PeriodStartUtc { get; init; }

	/// <summary>Number of rows whose timestamp falls into this bucket.</summary>
	public int Count { get; init; }
}

/// <summary>
/// Response wrapper for dashboard time-series charts. Buckets are ordered ascending by <see cref="StatsTimeseriesBucketDto.PeriodStartUtc"/>.
/// </summary>
public sealed class StatsTimeseriesResponseDto
{
	/// <summary>Metric key echoed from the request (e.g. users, messages).</summary>
	public string Metric { get; init; } = string.Empty;

	/// <summary>Either <c>day</c> or <c>week</c>.</summary>
	public string Bucket { get; init; } = string.Empty;

	public IReadOnlyList<StatsTimeseriesBucketDto> Buckets { get; init; } = Array.Empty<StatsTimeseriesBucketDto>();
}
