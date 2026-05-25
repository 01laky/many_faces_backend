namespace BeDemo.Api.Models.Requests.Stats;

public sealed class StatsTimeseriesQuery
{
	public string Metric { get; set; } = string.Empty;
	public DateTime FromUtc { get; set; }
	public DateTime ToUtc { get; set; }
	public string Bucket { get; set; } = "day";
}
