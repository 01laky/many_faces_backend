using System.Globalization;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Utils;

internal static class StatsTimeseriesBucketing
{
	public static IReadOnlyList<StatsTimeseriesBucketDto> BucketizeUtc(
		IReadOnlyList<DateTime> timestamps,
		DateTime fromUtc,
		DateTime toUtc,
		string bucket)
	{
		var counts = new Dictionary<DateTime, int>();

		foreach (var ts in timestamps)
		{
			var utc = DateTime.SpecifyKind(ts, DateTimeKind.Utc);
			var key = bucket == "week" ? StartOfIsoWeekUtc(utc) : utc.Date;
			counts.TryGetValue(key, out var c);
			counts[key] = c + 1;
		}

		var step = bucket == "week" ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);
		var start = bucket == "week" ? StartOfIsoWeekUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)) : fromUtc.Date;
		var end = bucket == "week" ? StartOfIsoWeekUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)) : toUtc.Date;

		var result = new List<StatsTimeseriesBucketDto>();
		for (var cursor = start; cursor <= end; cursor += step)
		{
			counts.TryGetValue(cursor, out var n);
			result.Add(new StatsTimeseriesBucketDto { PeriodStartUtc = cursor, Count = n });
		}

		return result;
	}

	private static DateTime StartOfIsoWeekUtc(DateTime utcInstant)
	{
		var utc = utcInstant.Kind == DateTimeKind.Utc ? utcInstant : utcInstant.ToUniversalTime();
		var year = ISOWeek.GetYear(utc);
		var week = ISOWeek.GetWeekOfYear(utc);
		return ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
	}
}
