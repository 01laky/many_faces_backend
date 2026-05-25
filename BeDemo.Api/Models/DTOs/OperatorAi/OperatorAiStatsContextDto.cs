using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Models.DTOs.OperatorAi;

/// <summary>
/// Full platform statistics bundle attached to operator AI chat (inline/live).
/// Only sent to gRPC for users who already passed <c>CanManageAllFaces</c>.
/// </summary>
public sealed class OperatorAiStatsContextDto
{
	public DateTime SnapshotUtc { get; init; }

	/// <summary>Same shape as <c>GET /api/Stats</c> — maximum aggregate counts for the model.</summary>
	public AdminDashboardSummaryDto Dashboard { get; init; } = new();

	/// <summary>Daily buckets for the last 7 UTC days (users, messages, stories) when the question is metrics-related.</summary>
	public OperatorAiTimeseriesHintsDto? TimeseriesLast7Days { get; init; }
}
