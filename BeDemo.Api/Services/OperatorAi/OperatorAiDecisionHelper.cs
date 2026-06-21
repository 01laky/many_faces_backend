using System.Text;
using BeDemo.Api.Configuration;
using BeDemo.Api.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi;

/// <summary>
/// 7B-perf O19 Role A — small routing/gating decisions for the operator AI. By default these are the deterministic
/// keyword heuristics (zero generations, zero latency). When a CPU-resident helper model is configured
/// (<see cref="AiServiceOptions.HelperModel"/>) and <see cref="OperatorAiOptions.HelperForDecisions"/> is on, the
/// helper refines the decision via a tiny single-label classification that runs on the CPU (advisory only — it never
/// changes numbers, only selects a path). The deterministic heuristic is always the fallback when the helper is unset
/// or unavailable, so behaviour degrades safely.
/// </summary>
public interface IOperatorAiDecisionHelper
{
	/// <summary>True for a genuinely simple single-metric count question (O2 fast-path gate).</summary>
	Task<bool> IsSimpleCountAsync(string message, CancellationToken cancellationToken = default);

	/// <summary>Detect the report type (face_health / moderation_backlog / grid_completeness) or null when ambiguous.</summary>
	Task<string?> DetectReportTypeAsync(string message, CancellationToken cancellationToken = default);

	/// <summary>
	/// LLM skill router (operator-ai LLM skill router): pick the registry id of the skill that should handle the
	/// message from the given candidates, or null when the helper is unset/unavailable/unparseable (the router then
	/// falls back to cosine). Candidates carry (Id, Label, Hint): the classifier sees the terse single-token Label +
	/// Hint, and the returned label maps back to the registry Id (so "general" → "general-assistant").
	/// </summary>
	Task<string?> DetectSkillAsync(
		string message,
		IReadOnlyList<(string Id, string Label, string Hint)> candidates,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// True when the message asks for a FULL/whole-system statistics overview (every entity), not one metric.
	/// Deterministic fallback = <see cref="OperatorAiStatsIntent.IsBroadOverviewQuestion"/>; the helper is consulted
	/// only to UPGRADE a keyword-miss (never to downgrade an explicit keyword), and is skipped for simple counts
	/// (never broad) and for single-entity questions (a question naming exactly one entity is never a whole-platform
	/// overview — the flagless single-entity broad-suppress, shared with <see cref="OperatorAiEntityDetection"/>).
	/// </summary>
	Task<bool> IsBroadOverviewAsync(string message, CancellationToken cancellationToken = default);

	/// <summary>Whether a helper model is configured + enabled (so callers can record it in the trace).</summary>
	bool HelperEnabled { get; }
}

/// <inheritdoc />
public sealed class OperatorAiDecisionHelper : IOperatorAiDecisionHelper
{
	private static readonly string[] ReportTypes = ["face_health", "moderation_backlog", "grid_completeness"];

	private readonly IAiGrpcService _ai;
	private readonly AiServiceOptions _aiOptions;
	private readonly OperatorAiOptions _options;
	private readonly ILogger<OperatorAiDecisionHelper> _logger;

	public OperatorAiDecisionHelper(
		IAiGrpcService ai,
		IOptions<AiServiceOptions> aiOptions,
		IOptions<OperatorAiOptions> options,
		ILogger<OperatorAiDecisionHelper> logger)
	{
		_ai = ai;
		_aiOptions = aiOptions.Value;
		_options = options.Value;
		_logger = logger;
	}

	public bool HelperEnabled =>
		_options.HelperForDecisions && !string.IsNullOrWhiteSpace(_aiOptions.HelperModel);

	public async Task<bool> IsSimpleCountAsync(string message, CancellationToken cancellationToken = default)
	{
		var deterministic = OperatorAiStatsIntent.IsSimpleCountQuestion(message);
		if (!HelperEnabled)
			return deterministic;

		// The helper only CONFIRMS a count classification; it can downgrade a deterministic "yes" to "no" when the
		// question is subtly qualitative, but never invents a number. On any helper issue we keep the deterministic
		// result (safe fallback).
		var prompt =
			"You are a strict classifier. Answer with exactly one word: YES or NO.\n"
			+ "Question: is the user's message a SIMPLE single-metric COUNT question whose answer is one number "
			+ "(not a comparison, trend, breakdown, average, or explanation)?\n\n"
			+ "User message: \"" + (message ?? string.Empty).Trim() + "\"\n\nAnswer (YES or NO):";

		var verdict = await ClassifyAsync(prompt, cancellationToken);
		if (verdict is null)
			return deterministic;
		return verdict.Value;
	}

	public async Task<string?> DetectReportTypeAsync(string message, CancellationToken cancellationToken = default)
	{
		var deterministic = OperatorAiReportTypeHeuristic.Detect(message);
		if (!HelperEnabled)
			return deterministic;

		var prompt =
			"You are a strict classifier. Choose exactly one label from this list that best matches the user's "
			+ "report request, or reply NONE if none fit: face_health, moderation_backlog, grid_completeness.\n\n"
			+ "User message: \"" + (message ?? string.Empty).Trim() + "\"\n\nLabel:";

		string raw;
		try
		{
			raw = await _ai.GenerateAsync(
				prompt,
				maxNewTokens: 6,
				temperature: 0.0,
				stopSequences: ["\n"],
				model: _aiOptions.HelperModel,
				cancellationToken: cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Decision helper report-type classification failed; using deterministic heuristic.");
			return deterministic;
		}

		var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
		foreach (var type in ReportTypes)
		{
			if (normalized.Contains(type, StringComparison.Ordinal))
				return type;
		}
		// Helper said NONE / unparalleled ⇒ defer to the deterministic heuristic rather than forcing a guess.
		return deterministic;
	}

	/// <inheritdoc />
	public async Task<string?> DetectSkillAsync(
		string message,
		IReadOnlyList<(string Id, string Label, string Hint)> candidates,
		CancellationToken cancellationToken = default)
	{
		// No deterministic baseline here: the caller (router) owns its own cosine fallback. Helper off ⇒ null.
		if (!HelperEnabled || candidates is null || candidates.Count == 0)
			return null;

		// Terse single-label classifier. We feed the short per-skill Label + Hint (NOT the verbose descriptor) and
		// ask for one bare label, so a weak 3B does not have to reproduce a hyphenated id like "general-assistant".
		var sb = new StringBuilder();
		sb.AppendLine("You are a strict router. Choose the ONE label whose area best fits the user's message.");
		sb.Append("Labels: ").AppendLine(string.Join(" | ", candidates.Select(c => c.Label)));
		foreach (var c in candidates)
			sb.Append("- ").Append(c.Label).Append(": ").AppendLine(c.Hint);
		sb.Append("User message: \"").Append((message ?? string.Empty).Trim()).AppendLine("\"");
		sb.Append("Answer with exactly one label:");

		string raw;
		try
		{
			raw = await _ai.GenerateAsync(
				sb.ToString(),
				maxNewTokens: 6,
				temperature: 0.0,
				stopSequences: ["\n"],
				model: _aiOptions.HelperModel,
				cancellationToken: cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Decision helper skill routing failed; router will fall back to cosine.");
			return null;
		}

		var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
		if (normalized.Length == 0)
			return null;
		// Map the returned label back to the registry id (e.g. "general" → "general-assistant"). Longest label first
		// so a label that is a prefix of another cannot shadow it.
		foreach (var c in candidates.OrderByDescending(c => c.Label.Length))
		{
			if (normalized.Contains(c.Label.ToLowerInvariant(), StringComparison.Ordinal))
				return c.Id;
		}
		return null; // unparseable / "none" ⇒ defer to cosine.
	}

	/// <inheritdoc />
	public async Task<bool> IsBroadOverviewAsync(string message, CancellationToken cancellationToken = default)
	{
		var deterministic = OperatorAiStatsIntent.IsBroadOverviewQuestion(message);

		// operator-ai conversational-context + broad-overview fix — FLAGLESS single-entity broad-suppress.
		// When the message names exactly ONE entity (word-boundary), a 3B "upgrade" to broad would be wrong: a
		// single-entity question is never a whole-platform overview. This also stops a carried/prepended follow-up
		// like "reels all active?" (metrics-positive via the "reel" keyword) from being flipped into the 61-bundle
		// dump. Because the `|| deterministic` short-circuit below runs first, this can ONLY apply when there is no
		// explicit broad keyword (deterministic == false here), so it NEVER overrides an explicit broad match — it
		// is a suppression of an unreliable upgrade, not the forbidden downgrade (§5). A flag is deliberately NOT
		// used: this method takes only a string, so a flag would force a signature change; a pure-string rule keeps
		// the public surface unchanged.
		var singleEntity = OperatorAiEntityDetection.DetectEntityBundleIndices(message).Count == 1;

		// Call the 3B ONLY when it can change the outcome: a keyword-miss that is still metrics-like, is NOT a
		// simple count (a simple count is never broad — OperatorAiStatsIntent.IsSimpleCountQuestion already excludes
		// broad), and does NOT name exactly one entity. Everything else is decided for free, keeping the common
		// focused/broad/count/single-entity cases off the helper.
		if (!HelperEnabled
			|| deterministic
			|| singleEntity
			|| !OperatorAiStatsIntent.IsMetricsQuestion(message)
			|| OperatorAiStatsIntent.IsSimpleCountQuestion(message))
			return deterministic;

		// Few-shot classifier (operator-ai broad-overview recall fix). The previous zero-shot prompt — and its
		// "rather than one specific metric" framing — led the small 3B to read "stats" in "give me full system
		// stats" as ONE metric and answer NO, so a clear whole-platform request fell through to a focused 4-bundle
		// answer. The labelled examples below (bilingual sk/en, balanced YES/NO, including the tricky single-entity
		// "all …" boundary) make the small model reliably promote whole-platform requests while keeping focused /
		// single-entity / single-count / non-analytics asks NO. Decode stays temperature 0 + YES/NO parse.
		var prompt =
			"You are a strict intent classifier for an admin analytics assistant.\n"
			+ "Classify the user's message as one of:\n"
			+ "  YES = a WHOLE-PLATFORM overview: counts/statistics across ALL or most data areas at once "
			+ "(a full snapshot of everything).\n"
			+ "  NO  = a FOCUSED ask: about ONE entity, ONE metric, a single count, or not analytics at all.\n"
			+ "Reply with exactly one word: YES or NO.\n\n"
			+ "Examples:\n"
			+ "  \"give me full system stats\" -> YES\n"
			+ "  \"give me everything\" -> YES\n"
			+ "  \"full statistics\" -> YES\n"
			+ "  \"all stats\" -> YES\n"
			+ "  \"all the numbers\" -> YES\n"
			+ "  \"all data about the platform\" -> YES\n"
			+ "  \"overview of all entities\" -> YES\n"
			+ "  \"system overview\" -> YES\n"
			+ "  \"everything in the system\" -> YES\n"
			+ "  \"a full snapshot of the whole platform\" -> YES\n"
			+ "  \"daj mi všetky štatistiky\" -> YES\n"
			+ "  \"celý prehľad systému\" -> YES\n"
			+ "  \"daj mi všetko o platforme\" -> YES\n"
			+ "  \"how many reels?\" -> NO\n"
			+ "  \"are all reels active?\" -> NO\n"
			+ "  \"reels pending vs approved\" -> NO\n"
			+ "  \"how many users?\" -> NO\n"
			+ "  \"blog stats\" -> NO\n"
			+ "  \"active reels\" -> NO\n"
			+ "  \"koľko máme reels?\" -> NO\n"
			+ "  \"koľko používateľov?\" -> NO\n"
			+ "  \"what time is it?\" -> NO\n"
			+ "  \"explain signalr\" -> NO\n\n"
			+ "User message: \"" + (message ?? string.Empty).Trim() + "\"\n"
			+ "Answer (YES or NO):";

		var verdict = await ClassifyAsync(prompt, cancellationToken);
		return verdict ?? deterministic; // NO / null / error ⇒ keep the deterministic (false) result.
	}

	/// <summary>Run a YES/NO helper classification; null when the helper is unavailable or the reply is unparseable.</summary>
	private async Task<bool?> ClassifyAsync(string prompt, CancellationToken cancellationToken)
	{
		string raw;
		try
		{
			raw = await _ai.GenerateAsync(
				prompt,
				maxNewTokens: 3,
				temperature: 0.0,
				stopSequences: ["\n"],
				model: _aiOptions.HelperModel,
				cancellationToken: cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Decision helper classification failed; using deterministic heuristic.");
			return null;
		}

		var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
		if (normalized.StartsWith("yes", StringComparison.Ordinal))
			return true;
		if (normalized.StartsWith("no", StringComparison.Ordinal))
			return false;
		return null;
	}
}

/// <summary>
/// Deterministic report-type heuristic shared by <see cref="OperatorAiDecisionHelper"/> and the reports skill, so the
/// fallback logic lives in one place (mirrors the previous in-skill detection).
/// </summary>
public static class OperatorAiReportTypeHeuristic
{
	public static string? Detect(string? message)
	{
		var m = (message ?? string.Empty).ToLowerInvariant();
		if (m.Contains("backlog", StringComparison.Ordinal) || (m.Contains("moderation", StringComparison.Ordinal) && (m.Contains("report", StringComparison.Ordinal) || m.Contains("queue", StringComparison.Ordinal))))
			return "moderation_backlog";
		if (m.Contains("grid", StringComparison.Ordinal) || m.Contains("completeness", StringComparison.Ordinal) || m.Contains("component", StringComparison.Ordinal))
			return "grid_completeness";
		if (m.Contains("face", StringComparison.Ordinal) && m.Contains("health", StringComparison.Ordinal))
			return "face_health";
		if (m.Contains("report", StringComparison.Ordinal))
		{
			if (m.Contains("moderation", StringComparison.Ordinal)) return "moderation_backlog";
			if (m.Contains("face", StringComparison.Ordinal)) return "face_health";
			if (m.Contains("grid", StringComparison.Ordinal)) return "grid_completeness";
		}
		return null;
	}
}
