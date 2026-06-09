using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Fallback skill chosen by the router when no data skill matches (below the routing threshold, §6.4). v1 is
/// minimal: a single context-free <c>Generate</c> reply with a system prompt that states it has no data for this
/// request and should answer briefly (and may list what it can help with). No retrieval, no data assembly; it must
/// not fabricate platform numbers. Trusted (no untrusted data). Registered id MUST be the registry's fallback id.
///
/// 7B-perf O4: its single terminal generation streams token by token (<see cref="IOperatorAiStreamingSkill"/>).
/// </summary>
public sealed class GeneralAssistantSkill : IOperatorAiStreamingSkill
{
	private const string DefaultReply =
		"I can help with platform statistics, admin reports, and the moderation backlog. What would you like to know?";

	private readonly IAiGrpcService _ai;
	private readonly OperatorAiOptions _options;

	public GeneralAssistantSkill(IAiGrpcService ai, IOptions<OperatorAiOptions> options)
	{
		_ai = ai;
		_options = options.Value;
	}

	public string Id => OperatorAiSkillRegistry.GeneralAssistantId; // "general-assistant"
	public string DisplayName => "General assistant";

	public string Description =>
		"General help and small talk for the operator when the request is not about specific platform data — "
		+ "greetings, what the assistant can do, how to phrase a question.";

	public IReadOnlyList<string> SampleRequests =>
		[
			"hello",
			"what can you help me with?",
			"how do I use this chat?",
			"thanks",
		];

	public OperatorAiSkillTrust Trust => OperatorAiSkillTrust.Trusted;

	public async Task<OperatorAiSkillResult> RunAsync(OperatorAiSkillRequest request, CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();

		var answer = await _ai.GenerateAsync(
			BuildPrompt(request.UserMessage),
			Math.Min(_options.MaxNewTokens, 256),
			responseLocale: "en",
			cancellationToken: cancellationToken);

		if (string.IsNullOrWhiteSpace(answer) || answer.StartsWith("Error:", StringComparison.Ordinal))
			answer = DefaultReply;

		sw.Stop();
		return new OperatorAiSkillResult(answer, Trace: Trace(sw, generations: 1));
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<OperatorAiStreamChunk> RunStreamingAsync(
		OperatorAiSkillRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();
		var acc = new StringBuilder();
		var sawError = false;

		await foreach (var delta in _ai.GenerateStreamAsync(
			BuildPrompt(request.UserMessage),
			Math.Min(_options.MaxNewTokens, 256),
			responseLocale: "en",
			cancellationToken: cancellationToken).WithCancellation(cancellationToken))
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
		var final = sawError || streamed.Length == 0 ? DefaultReply : streamed;
		yield return new OperatorAiStreamChunk(null, IsFinal: true, FinalAnswer: final, Trace: Trace(sw, generations: 1));
	}

	// No data context for this request — answer briefly in English and never invent platform numbers.
	private static string BuildPrompt(string userMessage) =>
		"You are the Many Faces operator assistant inside the admin app. You have NO platform data for this "
		+ "message — do not state or invent any counts, statistics or facts about the platform. Reply briefly "
		+ "and helpfully in English. If useful, mention that you can answer questions about platform statistics, "
		+ "generate admin reports, and summarize the moderation backlog.\n\n"
		+ $"Operator: {userMessage}\nAssistant:";

	private OperatorAiSkillTrace Trace(Stopwatch sw, int generations)
	{
		sw.Stop();
		return new OperatorAiSkillTrace(Id, UsedRetrieval: false, FellBackInternally: false, sw.ElapsedMilliseconds, FastPath: "general", Generations: generations);
	}
}
