using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Moderation Q&A skill (§6.3) — answers operator questions ABOUT moderation (backlog size/age, recent activity,
/// status breakdown) over **aggregate moderation metrics only**. It reads the existing pipeline's snapshot
/// (<see cref="IContentModerationMetrics"/>) — it does NOT run or replace the async review pipeline (D9). v1 pulls
/// no raw user content, so it is <see cref="OperatorAiSkillTrust.Trusted"/>; the untrusted "explain a specific
/// decision" excerpt path is deferred (§6.3).
///
/// 7B-perf O4: its single terminal generation streams token by token (<see cref="IOperatorAiStreamingSkill"/>).
/// </summary>
public sealed class ModerationSkill : IOperatorAiStreamingSkill
{
	private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	private readonly IContentModerationMetrics _metrics;
	private readonly IAiGrpcService _ai;
	private readonly OperatorAiOptions _options;

	public ModerationSkill(IContentModerationMetrics metrics, IAiGrpcService ai, IOptions<OperatorAiOptions> options)
	{
		_metrics = metrics;
		_ai = ai;
		_options = options.Value;
	}

	public string Id => "moderation";
	public string DisplayName => "Content moderation";

	public string Description =>
		"Answer questions about the content-moderation backlog and activity — how many submissions are pending, "
		+ "the oldest pending age, approved/rejected/removed counts, AI review job status, and top moderation flags.";

	public IReadOnlyList<string> SampleRequests =>
		[
			"how big is the moderation backlog?",
			"how old is the oldest pending submission?",
			"how many items were rejected?",
			"are there any failed AI review jobs?",
			"what are the top moderation flags?",
		];

	public string RouterHint =>
		"the content-moderation backlog/queue specifically — pending count, oldest pending age, "
		+ "approved/rejected/removed counts, AI-review job status, top moderation flags";

	public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted; // aggregate metrics only — no raw content (§6.3)

	public async Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();
		var (prompt, fallback) = await BuildAsync(request.UserMessage, cancellationToken);

		var answer = await _ai.GenerateAsync(prompt, _options.MaxNewTokens, responseLocale: "en", cancellationToken: cancellationToken);
		if (string.IsNullOrWhiteSpace(answer) || answer.StartsWith("Error:", StringComparison.Ordinal))
			answer = fallback;

		return new OperatorAiSkillResult(answer, Trace: Trace(sw));
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<OperatorAiStreamChunk> RunStreamingAsync(
		OperatorAiSkillRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();
		var (prompt, fallback) = await BuildAsync(request.UserMessage, cancellationToken);

		var acc = new StringBuilder();
		var sawError = false;
		await foreach (var delta in _ai.GenerateStreamAsync(
			prompt, _options.MaxNewTokens, responseLocale: "en", cancellationToken: cancellationToken).WithCancellation(cancellationToken))
		{
			if (delta.HasError) { sawError = true; break; }
			if (!string.IsNullOrEmpty(delta.TextDelta))
			{
				acc.Append(delta.TextDelta);
				yield return new OperatorAiStreamChunk(delta.TextDelta, IsFinal: false);
			}
			if (delta.IsFinal) break;
		}

		var streamed = acc.ToString().Trim();
		var final = sawError || streamed.Length == 0 ? fallback : streamed;
		yield return new OperatorAiStreamChunk(null, IsFinal: true, FinalAnswer: final, Trace: Trace(sw));
	}

	/// <summary>Assemble the aggregate-only moderation prompt + a deterministic fallback answer (no raw content / PII).</summary>
	private async Task<(string Prompt, string Fallback)> BuildAsync(string userMessage, CancellationToken cancellationToken)
	{
		var snap = await _metrics.GetSnapshotAsync(cancellationToken);

		var summary = new
		{
			snap.PendingSubmissions,
			snap.OldestPendingAgeHours,
			snap.ApprovedCount,
			snap.RejectedCount,
			snap.RemovedCount,
			snap.NeedsHumanReviewCount,
			snap.RecommendedApproveCount,
			snap.RecommendedRejectCount,
			snap.AiQueuedJobs,
			snap.AiProcessingJobs,
			snap.AiFailedJobs,
			snap.AverageReviewLatencyHours,
			TopFlags = snap.TopModerationFlags.Take(5).Select(f => new { f.Flag, f.Count }),
			PendingByFace = snap.PendingSubmissionsByFace.Take(5).Select(f => new { f.FaceTitle, f.PendingCount }),
		};
		var summaryJson = JsonSerializer.Serialize(summary, Json);

		var prompt =
			"You are the Many Faces operator moderation assistant. Answer the operator's question using ONLY the "
			+ "moderation summary below — these are aggregate counts, with no individual content. Reply concisely in "
			+ "English and do not invent numbers.\n\nModeration summary (JSON):\n"
			+ summaryJson
			+ $"\n\nOperator: {userMessage}\nAssistant:";

		return (prompt, $"Moderation backlog: {snap.PendingSubmissions} pending submission(s).");
	}

	private OperatorAiSkillTrace Trace(Stopwatch sw)
	{
		sw.Stop();
		return new OperatorAiSkillTrace(Id, UsedRetrieval: false, FellBackInternally: false, sw.ElapsedMilliseconds, FastPath: "moderation", Generations: 1);
	}
}
