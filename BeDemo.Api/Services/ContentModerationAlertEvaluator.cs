namespace BeDemo.Api.Services;

/// <summary>
/// Stateless translation from <see cref="ContentModerationMetricsSnapshot"/> to human-readable alerts.
/// Thresholds are intentionally simple starting points for logging and the admin UI; tune alongside product SLOs.
/// </summary>
public static class ContentModerationAlertEvaluator
{
    public const string OldestPendingExceeded = "oldest_pending_exceeded";
    public const string AiFailedJobs = "ai_failed_jobs";
    public const string HighPendingVolume = "high_pending_volume";

    public static IReadOnlyList<ModerationAlertDto> Evaluate(ContentModerationMetricsSnapshot snapshot, DateTime nowUtc)
    {
        _ = nowUtc; // Reserved for future time-windowed rules (e.g. business-hours comparisons).
        var alerts = new List<ModerationAlertDto>();
        // SLA-style signal: pending queue aging beyond one day.
        if (snapshot.OldestPendingAgeHours is >= 24)
        {
            alerts.Add(new ModerationAlertDto(
                OldestPendingExceeded,
                "warning",
                $"Oldest pending submission is about {Math.Round(snapshot.OldestPendingAgeHours.Value)} hours old."));
        }

        // Several failed jobs usually means infra or model instability rather than a single bad item.
        if (snapshot.AiFailedJobs >= 3)
        {
            alerts.Add(new ModerationAlertDto(
                AiFailedJobs,
                "warning",
                $"{snapshot.AiFailedJobs} AI review jobs are in a failed state."));
        }

        // Informational backlog indicator for capacity planning (not necessarily an error).
        if (snapshot.PendingSubmissions >= 50)
        {
            alerts.Add(new ModerationAlertDto(
                HighPendingVolume,
                "info",
                $"Pending submissions backlog is {snapshot.PendingSubmissions} items."));
        }

        return alerts;
    }
}

/// <param name="Code">Stable machine identifier for clients and log queries.</param>
/// <param name="Severity">Loosely matches syslog-style levels (warning, info, etc.).</param>
/// <param name="Message">End-user/admin readable explanation.</param>
public sealed record ModerationAlertDto(string Code, string Severity, string Message);
