using BeDemo.Api.Models.Requests.Stats;
using FluentValidation;

namespace BeDemo.Api.Validation.Stats;

/// <summary>GET /api/Stats/timeseries (§11.18).</summary>
public sealed class StatsTimeseriesQueryValidator : AbstractValidator<StatsTimeseriesQuery>
{
	private static readonly string[] Metrics =
	[
		"users", "messages", "stories", "blogs", "reels", "albums", "friendrequests", "walltickets",
	];
	private static readonly string[] Buckets = ["day", "week"];

	public StatsTimeseriesQueryValidator()
	{
		RuleFor(x => x.Metric).NotEmpty().Must(m => Metrics.Contains(m, StringComparer.OrdinalIgnoreCase));
		RuleFor(x => x.Bucket).Must(b => Buckets.Contains(b, StringComparer.OrdinalIgnoreCase));
		RuleFor(x => x.ToUtc).GreaterThanOrEqualTo(x => x.FromUtc);
		RuleFor(x => x).Must(q => (q.ToUtc - q.FromUtc).TotalDays <= ValidationConstants.StatsMaxSpanDays);
	}
}
