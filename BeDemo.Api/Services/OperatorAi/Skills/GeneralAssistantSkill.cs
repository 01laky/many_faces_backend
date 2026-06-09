using System.Diagnostics;
using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <summary>
/// Fallback skill chosen by the router when no data skill matches (below the routing threshold, §6.4). v1 is
/// minimal: a single context-free <c>Generate</c> reply with a system prompt that states it has no data for this
/// request and should answer briefly (and may list what it can help with). No retrieval, no data assembly; it must
/// not fabricate platform numbers. Trusted (no untrusted data). Registered id MUST be the registry's fallback id.
/// </summary>
public sealed class GeneralAssistantSkill : IOperatorAiSkill
{
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

		// No data context for this request — answer briefly in English and never invent platform numbers.
		var prompt =
			"You are the Many Faces operator assistant inside the admin app. You have NO platform data for this "
			+ "message — do not state or invent any counts, statistics or facts about the platform. Reply briefly "
			+ "and helpfully in English. If useful, mention that you can answer questions about platform statistics, "
			+ "generate admin reports, and summarize the moderation backlog.\n\n"
			+ $"Operator: {request.UserMessage}\nAssistant:";

		var answer = await _ai.GenerateAsync(
			prompt,
			Math.Min(_options.MaxNewTokens, 256),
			responseLocale: "en",
			cancellationToken: cancellationToken);

		if (string.IsNullOrWhiteSpace(answer))
			answer = "I can help with platform statistics, admin reports, and the moderation backlog. What would you like to know?";

		sw.Stop();
		return new OperatorAiSkillResult(answer, Trace: new OperatorAiSkillTrace(Id, UsedRetrieval: false, FellBackInternally: false, sw.ElapsedMilliseconds));
	}
}
