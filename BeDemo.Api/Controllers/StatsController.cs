using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Models.Requests.Stats;
using BeDemo.Api.Security;
using BeDemo.Api.Services;

namespace BeDemo.Api.Controllers;

/// <summary>
/// Platform dashboard statistics for the admin SPA: consolidated counts (<c>GET /api/Stats</c>), optional
/// UTC histograms (<c>GET /api/Stats/timeseries</c>), and anonymous aggregate totals (<c>GET /api/Stats/public</c>).
/// </summary>
// Backend-refactor X5/X6: the two operator-only endpoints (GetStats, GetTimeseries) carry a method-level
// [Authorize(Policy = ManageAllFaces)] instead of an in-body CanManageAllFaces check; the public aggregate endpoint
// keeps its own [AllowAnonymous]. Class-level [Authorize] still applies the default authenticated fallback.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StatsController : ControllerBase
{
	private readonly ApplicationDbContext _context;
	private readonly IPlatformStatsQueryService _statsQuery;

	public StatsController(
		ApplicationDbContext context,
		IPlatformStatsQueryService statsQuery)
	{
		_context = context;
		_statsQuery = statsQuery;
	}

	/// <summary>
	/// Returns the full <see cref="AdminDashboardSummaryDto"/> for authorized platform operators.
	/// </summary>
	[HttpGet]
	[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
	[ProducesResponseType(typeof(AdminDashboardSummaryDto), StatusCodes.Status200OK)]
	public async Task<ActionResult<AdminDashboardSummaryDto>> GetStats(CancellationToken cancellationToken)
	{
		return Ok(await _statsQuery.GetOperatorDashboardSummaryAsync(cancellationToken));
	}

	/// <summary>
	/// Anonymous aggregate counts only (no OAuth, moderation audit, or per-user data). Use the <b>public</b> face
	/// URL prefix so <see cref="Middlewares.FaceScopeEnforcementMiddleware"/> allows unauthenticated access.
	/// </summary>
	[HttpGet("public")]
	[AllowAnonymous]
	[ProducesResponseType(typeof(PublicStatsSnapshotDto), StatusCodes.Status200OK)]
	public async Task<ActionResult<PublicStatsSnapshotDto>> GetPublicStats(CancellationToken cancellationToken)
	{
		Response.Headers.CacheControl = "public, max-age=60";
		return Ok(await _statsQuery.GetPublicSnapshotAsync(cancellationToken));
	}

	/// <summary>
	/// Histogram data for dashboard charts.
	/// </summary>
	[HttpGet("timeseries")]
	[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]
	[ProducesResponseType(typeof(StatsTimeseriesResponseDto), StatusCodes.Status200OK)]
	public async Task<ActionResult<StatsTimeseriesResponseDto>> GetTimeseries(
		[FromQuery] StatsTimeseriesQuery query,
		CancellationToken cancellationToken = default)
	{
		var m = query.Metric.Trim().ToLowerInvariant();
		var fromUtc = query.FromUtc;
		var toUtc = query.ToUtc;
		var b = query.Bucket.Trim().ToLowerInvariant();

		List<DateTime> timestamps = m switch
		{
			"users" => await _context.Users.AsNoTracking()
				.Where(u => u.CreatedAt >= fromUtc && u.CreatedAt <= toUtc)
				.Select(u => u.CreatedAt)
				.ToListAsync(cancellationToken),
			"messages" => await _context.Messages.AsNoTracking()
				.Where(x => x.SentAt >= fromUtc && x.SentAt <= toUtc)
				.Select(x => x.SentAt)
				.ToListAsync(cancellationToken),
			"stories" => await _context.Stories.AsNoTracking()
				.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
				.Select(x => x.CreatedAt)
				.ToListAsync(cancellationToken),
			"blogs" => await _context.Blogs.AsNoTracking()
				.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
				.Select(x => x.CreatedAt)
				.ToListAsync(cancellationToken),
			"reels" => await _context.Reels.AsNoTracking()
				.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
				.Select(x => x.CreatedAt)
				.ToListAsync(cancellationToken),
			"albums" => await _context.Albums.AsNoTracking()
				.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
				.Select(x => x.CreatedAt)
				.ToListAsync(cancellationToken),
			"friendrequests" => await _context.FriendRequests.AsNoTracking()
				.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
				.Select(x => x.CreatedAt)
				.ToListAsync(cancellationToken),
			"walltickets" => await _context.FaceWallTickets.AsNoTracking()
				.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt <= toUtc)
				.Select(x => x.CreatedAt)
				.ToListAsync(cancellationToken),
			_ => throw new InvalidOperationException("Unreachable metric after validation."),
		};

		var buckets = BucketizeUtc(timestamps, fromUtc, toUtc, b);
		return Ok(new StatsTimeseriesResponseDto
		{
			Metric = m,
			Bucket = b,
			Buckets = buckets,
		});
	}

	private static IReadOnlyList<StatsTimeseriesBucketDto> BucketizeUtc(
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
