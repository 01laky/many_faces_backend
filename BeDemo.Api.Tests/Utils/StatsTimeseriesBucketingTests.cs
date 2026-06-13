using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

/// <summary>
/// Edge-case coverage for the stats timeseries bucketing (previously untested): the day/week histogram
/// gap-fills the full range, counts multiple events per bucket, and aligns week buckets to ISO Mondays.
/// </summary>
public sealed class StatsTimeseriesBucketingTests
{
	private static DateTime Utc(int y, int m, int d, int h = 0) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Day_buckets_gap_fill_the_full_range_with_zero_counts()
	{
		var result = StatsTimeseriesBucketing.BucketizeUtc(
			new List<DateTime>(),
			Utc(2026, 1, 1),
			Utc(2026, 1, 3),
			"day");

		result.Should().HaveCount(3);
		result.Select(b => b.Count).Should().Equal(0, 0, 0);
		result[0].PeriodStartUtc.Should().Be(Utc(2026, 1, 1));
		result[2].PeriodStartUtc.Should().Be(Utc(2026, 1, 3));
	}

	[Fact]
	public void Day_buckets_count_events_per_day_and_leave_empty_days_zero()
	{
		var timestamps = new List<DateTime>
		{
			Utc(2026, 1, 1, 5),
			Utc(2026, 1, 1, 9),
			Utc(2026, 1, 3, 1),
		};

		var result = StatsTimeseriesBucketing.BucketizeUtc(timestamps, Utc(2026, 1, 1), Utc(2026, 1, 3), "day");

		result.Select(b => b.Count).Should().Equal(2, 0, 1);
	}

	[Fact]
	public void Single_day_range_yields_one_bucket()
	{
		var result = StatsTimeseriesBucketing.BucketizeUtc(
			new List<DateTime> { Utc(2026, 6, 14, 12) },
			Utc(2026, 6, 14),
			Utc(2026, 6, 14),
			"day");

		result.Should().ContainSingle();
		result[0].Count.Should().Be(1);
	}

	[Fact]
	public void Week_buckets_align_to_iso_mondays_and_preserve_total_counts()
	{
		var timestamps = new List<DateTime>
		{
			Utc(2026, 1, 6),
			Utc(2026, 1, 8),
			Utc(2026, 1, 14),
		};

		var result = StatsTimeseriesBucketing.BucketizeUtc(timestamps, Utc(2026, 1, 1), Utc(2026, 1, 15), "week");

		result.Should().NotBeEmpty();
		result.Should().OnlyContain(b => b.PeriodStartUtc.DayOfWeek == DayOfWeek.Monday);
		result.Sum(b => b.Count).Should().Be(3);
	}
}
