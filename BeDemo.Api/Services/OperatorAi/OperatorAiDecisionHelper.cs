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
