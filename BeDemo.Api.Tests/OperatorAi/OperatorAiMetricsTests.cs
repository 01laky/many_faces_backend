using System.Diagnostics.Metrics;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai degraded-handling D19 — the PII-free degradation metrics. Subscribes a <see cref="MeterListener"/> to
/// the <c>ManyFaces.OperatorAi</c> meter and asserts the degraded-turn counter (tagged by code) and the per-bundle
/// produced/failed counters record the expected aggregates, so monitoring can derive a degraded rate + failure rate.
/// </summary>
public sealed class OperatorAiMetricsTests
{
	private sealed record Measurement(string Instrument, long Value, IReadOnlyDictionary<string, object?> Tags);

	private static List<Measurement> Collect(Action act)
	{
		var measurements = new List<Measurement>();
		using var listener = new MeterListener
		{
			InstrumentPublished = (instrument, l) =>
			{
				if (instrument.Meter.Name == OperatorAiMetrics.MeterName)
					l.EnableMeasurementEvents(instrument);
			},
		};
		listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
		{
			var map = new Dictionary<string, object?>();
			foreach (var t in tags)
				map[t.Key] = t.Value;
			lock (measurements)
				measurements.Add(new Measurement(instrument.Name, value, map));
		});
		listener.Start();
		act();
		listener.Dispose();
		return measurements;
	}

	[Fact]
	public void RecordDegradedTurn_increments_counter_tagged_by_code()
	{
		var measurements = Collect(() =>
		{
			OperatorAiMetrics.RecordDegradedTurn("ai_unavailable");
			OperatorAiMetrics.RecordDegradedTurn("model_loading");
		});

		var degraded = measurements.Where(m => m.Instrument == "operator_ai.degraded_turns").ToList();
		degraded.Should().HaveCount(2);
		degraded.Should().ContainSingle(m => (string?)m.Tags["code"] == "ai_unavailable" && m.Value == 1);
		degraded.Should().ContainSingle(m => (string?)m.Tags["code"] == "model_loading" && m.Value == 1);
	}

	[Fact]
	public void RecordBundleOutcomes_emits_produced_and_failed_with_outcome_tag()
	{
		var measurements = Collect(() => OperatorAiMetrics.RecordBundleOutcomes(produced: 3, failed: 2));

		var sections = measurements.Where(m => m.Instrument == "operator_ai.bundle_sections").ToList();
		sections.Should().ContainSingle(m => (string?)m.Tags["outcome"] == "produced" && m.Value == 3);
		sections.Should().ContainSingle(m => (string?)m.Tags["outcome"] == "failed" && m.Value == 2);
	}

	[Fact]
	public void RecordBundleOutcomes_skips_zero_counts()
	{
		var measurements = Collect(() => OperatorAiMetrics.RecordBundleOutcomes(produced: 0, failed: 0));
		measurements.Where(m => m.Instrument == "operator_ai.bundle_sections").Should().BeEmpty();
	}
}
