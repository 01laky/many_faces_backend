using BeDemo.Api.Configuration;
using BeDemo.Api.Hubs;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai degraded failure-handling (Phase 1), exhaustive edge cases. When per-bundle generations fail the gRPC
/// client returns an "Error: …" STRING (not an exception); these tests pin: a failed/empty section is dropped (clean
/// "data unavailable" note, no error text leaking into the answer / synthesis prompt — D1/D2); an all-MODEL-failed
/// turn returns the honest infrastructure sentinel while an all-DATA-missing turn does NOT (the D3 distinction); a
/// partial failure adds a deterministic coverage note (D4, focused + broad wording); a transient blip is absorbed by
/// the bounded retry (D13); and a hard error early-aborts the remaining maps without dropping already-produced
/// sections (D14). Synthesis is OFF in most tests so the deterministic stitch is asserted directly; one test turns it
/// ON to assert the error string never reaches the synthesis prompt.
/// </summary>
public sealed class OperatorAiDegradedDataHandlingTests
{
	private static OperatorAiOptions DefaultOptions(bool synthesis = false, bool broadDeterministic = false) => new()
	{
		MaxParallelBundleAiCalls = 2,
		PerBundleGenerateTimeoutMs = 2000,
		OverallTurnBudgetMs = 10000,
		LiveUseAiSynthesisStitch = synthesis,
		LiveBroadDeterministicCounts = broadDeterministic,
	};

	/// <summary>A Ready bundle row with a usable totalCount payload (the normal "data is here" case).</summary>
	private static OperatorAiBundleCacheEntry ReadyEntry(int i) =>
		new(i, OperatorAiEntityBundleCatalog.GetByIndex(i).Id, OperatorAiBundleCacheState.Ready,
			"{\"totalCount\":5}", null, DateTime.UtcNow, DateTime.UtcNow);

	/// <summary>
	/// A Ready row with an EMPTY payload — the orchestrator drops it as Failed WITHOUT setting AiError, i.e. a DATA gap
	/// (bundle JSON not ready), NOT a model failure. The whole point of D3 is that this must never be misread as
	/// "AI unavailable". No Generate is ever issued for such a bundle.
	/// </summary>
	private static OperatorAiBundleCacheEntry DataMissingEntry(int i) =>
		new(i, OperatorAiEntityBundleCatalog.GetByIndex(i).Id, OperatorAiBundleCacheState.Ready,
			string.Empty, null, DateTime.UtcNow, DateTime.UtcNow);

	private static OperatorAiLiveStatsOrchestrator Build(
		Mock<IAiGrpcService> ai,
		OperatorAiOptions? options = null,
		Func<int, OperatorAiBundleCacheEntry>? entryFor = null)
	{
		entryFor ??= ReadyEntry;
		var decisions = new Mock<IOperatorAiDecisionHelper>();
		decisions.Setup(d => d.IsSimpleCountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
		var prefetcher = new Mock<IOperatorAiLiveStatsPrefetcher>();
		prefetcher.Setup(p => p.PrefetchSelectedAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IReadOnlyList<int> idx, CancellationToken _) =>
				new OperatorAiLiveStatsPrefetchResult(
					idx.ToDictionary(i => i, entryFor), idx.Count, 0, idx.Count, 0));
		return new OperatorAiLiveStatsOrchestrator(
			prefetcher.Object, ai.Object, decisions.Object, Options.Create(options ?? DefaultOptions()),
			Options.Create(new AiServiceOptions()), NullLogger<OperatorAiLiveStatsOrchestrator>.Instance);
	}

	private static void Generates(Mock<IAiGrpcService> ai, Func<string, string> byPrompt) =>
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((string prompt, int _, string? _, string? _, double? _, IReadOnlyList<string>? _, string? _, CancellationToken _) => byPrompt(prompt));

	[Fact]
	public async Task Per_bundle_error_string_is_dropped_and_never_leaks_into_the_answer()
	{
		var ai = new Mock<IAiGrpcService>();
		// index 0 = entity.users (succeeds); index 1 = entity.userProfiles (the worker returns the "Error:" string).
		Generates(ai, prompt => prompt.Contains("entity.userProfiles")
			? "Error: AI service unavailable (Unavailable)"
			: "Users: 5 total.");

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Contain("Users: 5 total.");
		result.Should().NotContain("Error:", "an error-string generation must never reach the user-facing answer");
		result.Should().Contain("Data unavailable", "the failed section degrades to a clean note");
		result.Should().Contain("1 of 2 data area(s)", "a partial failure adds a deterministic coverage note (D4)");
	}

	[Fact]
	public async Task All_bundles_failing_returns_the_honest_unavailable_sentinel_not_a_wall()
	{
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, _ => "Error: AI service unavailable (Unavailable)");

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Be(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);
		result.Should().NotContain("Data unavailable", "all-failed returns one honest message, not a wall of per-bundle notes");
		OperatorAiResponseGuard.ShouldNotPersist(result).Should().BeTrue("the sentinel must route to the ephemeral path, not be persisted");
	}

	[Fact]
	public async Task Hard_model_error_early_aborts_the_remaining_bundle_maps_D14()
	{
		// D14 — when the FIRST mapped bundle returns a hard "Error:" (model down) that survives the bounded retry, the
		// remaining queued maps must NOT fire: we go straight to the honest all-failed sentinel. With parallelism pinned
		// to 1 the maps run serially, so far fewer than one-Generate-per-bundle (here 5) are issued.
		var ai = new Mock<IAiGrpcService>();
		var calls = 0;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() =>
			{
				Interlocked.Increment(ref calls);
				return "Error: AI service unavailable (Unavailable)";
			});

		var indices = new[] { 0, 1, 2, 3, 4 };
		var result = await Build(ai).RunWithSelectedIndicesAsync("counts of everything?", indices, 1, appendCoverageNote: false);

		result.Should().Be(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);
		calls.Should().BeLessThan(indices.Length,
			"early-abort stops the remaining bundle maps once the model is known down — no per-bundle Generate for all 5");
	}

	[Fact]
	public async Task Healthy_multi_bundle_turn_is_unchanged()
	{
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, _ => "ok facts.");

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Contain("ok facts.");
		result.Should().NotContain("Data unavailable");
		result.Should().NotContain("could not be loaded");
		OperatorAiResponseGuard.ShouldNotPersist(result).Should().BeFalse();
	}

	// ── D1 — an EMPTY generation (not an "Error:" string) is still dropped as a failed section ─────────────────
	[Fact]
	public async Task Empty_generation_is_dropped_as_a_failed_section_partial()
	{
		var ai = new Mock<IAiGrpcService>();
		// index 0 succeeds; index 1 (entity.userProfiles) returns an EMPTY string — usable-but-empty, not "Error:".
		Generates(ai, prompt => prompt.Contains("entity.userProfiles") ? string.Empty : "Users: 5 total.");

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Contain("Users: 5 total.");
		result.Should().Contain("Data unavailable", "an empty generation degrades to the same clean note as an error");
		result.Should().Contain("1 of 2 data area(s)");
		result.Should().NotContain("Error:");
	}

	// ── D3 — the critical distinction: DATA-not-ready bundles must NOT be reported as "AI unavailable" ─────────
	[Fact]
	public async Task All_data_missing_bundles_do_not_return_the_ai_unavailable_sentinel()
	{
		// No bundle has a usable payload (DB JSON not ready). Each fails WITHOUT AiError, so the turn is a DATA gap,
		// not a model outage: it must render the deterministic "data unavailable" read-out, never the AI sentinel,
		// and the model is never even called.
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, _ => "should never be called");

		var result = await Build(ai, entryFor: DataMissingEntry)
			.RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().NotBe(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel, "data-not-ready is not an AI failure");
		result.Should().Contain("Data unavailable");
		result.Should().Contain("2 of 2 data area(s)");
		OperatorAiResponseGuard.ShouldNotPersist(result).Should().BeFalse("a data-gap read-out is a legitimate answer, not an infra ephemeral");
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	[Fact]
	public async Task All_empty_generations_return_the_sentinel_but_without_early_abort()
	{
		// Every bundle is Ready (data present) but the model returns "" for all → a MODEL failure (empty ⇒ AiError),
		// so the sentinel fires. But empty is NOT a hard "Error:", so early-abort does NOT trigger — every bundle is
		// mapped (2 attempts each), unlike the all-"Error:" case which aborts after the first.
		var ai = new Mock<IAiGrpcService>();
		var calls = 0;
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => { Interlocked.Increment(ref calls); return string.Empty; });

		var indices = new[] { 0, 1, 2 };
		var result = await Build(ai).RunWithSelectedIndicesAsync("counts?", indices, 1, appendCoverageNote: false);

		result.Should().Be(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);
		calls.Should().Be(indices.Length * 2, "empty does not early-abort: all 3 bundles map with the bounded retry (3×2)");
	}

	[Fact]
	public async Task Mixed_model_error_plus_data_missing_all_failed_returns_the_sentinel()
	{
		// index 0 is Ready but the model errors; index 1 has no payload (data gap). Nothing was produced and at least
		// one failure is a MODEL error ⇒ the honest AI-unavailable sentinel (a single model error dominates a data gap).
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, _ => "Error: AI service unavailable (Unavailable)");

		var result = await Build(ai, entryFor: i => i == 0 ? ReadyEntry(i) : DataMissingEntry(i))
			.RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Be(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);
	}

	// ── D13 — a single transient blip is absorbed by the bounded retry, the section is NOT dropped ─────────────
	[Fact]
	public async Task Transient_per_bundle_error_is_recovered_by_the_bounded_retry()
	{
		var ai = new Mock<IAiGrpcService>();
		var profilesAttempts = 0;
		// entity.userProfiles errors on the first attempt, succeeds on the retry; the other bundle always succeeds.
		// Two bundles are used so the per-bundle MAP path (where the retry lives) runs, not the single-bundle
		// fast-path. NB: "entity.userProfiles" is the discriminator because "entity.users" is a substring of it.
		Generates(ai, prompt =>
		{
			if (prompt.Contains("entity.userProfiles"))
				return Interlocked.Increment(ref profilesAttempts) == 1 ? "Error: transient blip" : "Profiles: 5 total.";
			return "Users: 5 total.";
		});

		var result = await Build(ai).RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 2, appendCoverageNote: false);

		result.Should().Contain("Profiles: 5 total.", "the retry recovered the transient blip — the section is not dropped");
		result.Should().Contain("Users: 5 total.");
		result.Should().NotContain("Data unavailable");
		result.Should().NotContain("could not be loaded");
		result.Should().NotBe(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);
		profilesAttempts.Should().Be(2, "exactly one retry — the bounded retry is a single extra attempt, no thrashing");
	}

	// ── D14 — early-abort keeps sections already produced; it does not force the all-failed sentinel ───────────
	[Fact]
	public async Task Early_abort_keeps_already_produced_sections_and_does_not_force_the_sentinel()
	{
		var ai = new Mock<IAiGrpcService>();
		var calls = 0;
		// Serial (parallel=1) so index 0 completes first: success → then index 1 (entity.userProfiles) hard-errors and
		// aborts index 2. Discriminate on "entity.userProfiles" (unique) so only index 1 errors.
		Generates(ai, prompt =>
		{
			Interlocked.Increment(ref calls);
			return prompt.Contains("entity.userProfiles") ? "Error: AI service unavailable (Unavailable)" : "Section facts here.";
		});

		var result = await Build(ai).RunWithSelectedIndicesAsync("everything?", new[] { 0, 1, 2 }, 1, appendCoverageNote: false);

		result.Should().Contain("Section facts here.", "an already-produced section survives the early-abort");
		result.Should().NotBe(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel, "a partial success is not an all-failed turn");
		result.Should().Contain("could not be loaded");
		result.Should().NotContain("Error:");
		calls.Should().BeLessThan(4, "index 0 (1 call) + index 1 (≤2 retry) fire; index 2 is aborted before any Generate");
	}

	// ── D2 — the error string never reaches the AI synthesis prompt (synthesis ON) ─────────────────────────────
	[Fact]
	public async Task Error_string_never_reaches_the_synthesis_prompt_when_synthesis_is_on()
	{
		var ai = new Mock<IAiGrpcService>();
		var prompts = new List<string>();
		Generates(ai, prompt =>
		{
			lock (prompts)
				prompts.Add(prompt);
			if (prompt.Contains("Facts from database bundles:"))
				return "Synthesized: 5 users; profile data was unavailable.";
			if (prompt.Contains("entity.userProfiles"))
				return "Error: AI service unavailable (Unavailable)";
			return "Users: 5 total.";
		});

		// parallel=1 so index 0 (success) completes before index 1 errors → a partial draft that still synthesizes.
		var result = await Build(ai, DefaultOptions(synthesis: true))
			.RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 1, appendCoverageNote: false);

		result.Should().Be("Synthesized: 5 users; profile data was unavailable.");
		var synthesisPrompt = prompts.Single(p => p.Contains("Facts from database bundles:"));
		synthesisPrompt.Should().NotContain("Error:", "D2 — the failed section is rendered as a clean note, the error string is dropped before stitch");
		synthesisPrompt.Should().Contain("Data unavailable");
		synthesisPrompt.Should().Contain("Users: 5 total.");
	}

	// ── D4 — broad-overview partial uses its own "temporarily unavailable" coverage wording ────────────────────
	[Fact]
	public async Task Broad_overview_partial_failure_uses_the_full_snapshot_coverage_wording()
	{
		// Broad deterministic snapshot renders each bundle from its JSON (0 Generates). index 1 has no totalCount, so
		// it is the only "unavailable" data area and drives the broad-specific coverage note.
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, _ => "should never be called for a deterministic broad snapshot");

		var result = await Build(
				ai,
				DefaultOptions(broadDeterministic: true),
				entryFor: i => i == 1 ? DataMissingEntry(i) : ReadyEntry(i))
			.RunWithSelectedIndicesAsync("full platform overview", new[] { 0, 1, 2 }, 2, appendCoverageNote: true, broadOverview: true);

		result.Should().Contain("Full platform snapshot");
		result.Should().Contain("1 data area(s) were temporarily unavailable");
		result.Should().NotBe(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel, "a broad snapshot with partial data is not an AI outage");
		ai.Verify(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
			It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
	}

	// ── Optional — all-or-nothing partial-failure policy (config-gated, default off) ───────────────────────────
	[Fact]
	public async Task Partial_failure_returns_the_sentinel_when_all_or_nothing_policy_is_enabled()
	{
		// Default (flag off) keeps the partial facts + coverage note — pinned by
		// Per_bundle_error_string_is_dropped_and_never_leaks_into_the_answer. With the all-or-nothing flag ON, the
		// same partial AI failure is treated as a whole-turn outage and returns the honest unavailable sentinel.
		var ai = new Mock<IAiGrpcService>();
		Generates(ai, prompt => prompt.Contains("entity.userProfiles") ? "Error: AI service unavailable (Unavailable)" : "Users: 5 total.");
		var options = DefaultOptions();
		options.LivePartialFailureAllOrNothing = true;

		var result = await Build(ai, options)
			.RunWithSelectedIndicesAsync("how many users and profiles?", new[] { 0, 1 }, 1, appendCoverageNote: false);

		result.Should().Be(OperatorAiLiveStatsOrchestrator.AllBundlesFailedSentinel);
		result.Should().NotContain("Users: 5 total.", "all-or-nothing must not leak partial facts");
	}
}
