using System.Diagnostics.Metrics;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// operator-ai degraded-handling D19 — PII-free observability for degraded turns + per-bundle failure rate, so the
/// operator-AI degradation (the demo-day failure mode) is caught proactively by monitoring rather than at the next
/// demo. Uses the standard <see cref="System.Diagnostics.Metrics"/> API (no extra dependency): the counters are
/// cheap no-ops until something — an OpenTelemetry exporter, a <see cref="MeterListener"/>, or a test — subscribes to
/// the <c>ManyFaces.OperatorAi</c> meter. Only aggregate counts + a low-cardinality failure-code tag are recorded;
/// never message bodies, user ids, or any content.
/// </summary>
public static class OperatorAiMetrics
{
	/// <summary>The meter name external collectors subscribe to.</summary>
	public const string MeterName = "ManyFaces.OperatorAi";

	private static readonly Meter Meter = new(MeterName);

	/// <summary>Count of operator-AI turns that degraded (an ephemeral was sent instead of a persisted answer), tagged by failure code.</summary>
	private static readonly Counter<long> DegradedTurns =
		Meter.CreateCounter<long>("operator_ai.degraded_turns", unit: "{turn}", description: "Operator-AI turns that degraded to an ephemeral, by failure code.");

	/// <summary>Count of per-bundle map sections that were produced vs failed, so the per-bundle failure RATE can be derived.</summary>
	private static readonly Counter<long> BundleSections =
		Meter.CreateCounter<long>("operator_ai.bundle_sections", unit: "{section}", description: "Operator-AI per-bundle map sections, tagged outcome=produced|failed.");

	/// <summary>Record a degraded turn (the hub sent an honest ephemeral and did not persist a half-answer). <paramref name="code"/> is the low-cardinality hub error code.</summary>
	public static void RecordDegradedTurn(string code) =>
		DegradedTurns.Add(1, new KeyValuePair<string, object?>("code", code));

	/// <summary>
	/// Record the outcome of one multi-bundle map turn: how many sections were produced vs failed. The per-bundle
	/// failure rate is then <c>failed / (produced + failed)</c> across turns. A no-op when there were no sections.
	/// </summary>
	public static void RecordBundleOutcomes(int produced, int failed)
	{
		if (produced > 0)
			BundleSections.Add(produced, new KeyValuePair<string, object?>("outcome", "produced"));
		if (failed > 0)
			BundleSections.Add(failed, new KeyValuePair<string, object?>("outcome", "failed"));
	}
}
