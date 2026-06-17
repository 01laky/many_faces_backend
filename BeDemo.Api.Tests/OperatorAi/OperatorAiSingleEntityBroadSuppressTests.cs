using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai conversational-context + broad-overview fix — the FLAGLESS single-entity broad-suppress inside
/// <see cref="OperatorAiDecisionHelper.IsBroadOverviewAsync"/>. The helper here is configured ENABLED and rigged to
/// answer "YES" to every classification, so each test proves whether the 3B upgrade was consulted or short-circuited.
/// </summary>
public sealed class OperatorAiSingleEntityBroadSuppressTests
{
	// A helper that, IF asked, always upgrades to broad ("YES") — so a `false` result can only mean it was suppressed.
	private static OperatorAiDecisionHelper EnabledYesHelper(Mock<IAiGrpcService> ai)
	{
		ai.Setup(a => a.GenerateAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
				It.IsAny<double?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync("YES");
		return new OperatorAiDecisionHelper(
			ai.Object,
			Options.Create(new AiServiceOptions { HelperModel = "qwen2.5:3b" }),
			Options.Create(new OperatorAiOptions { HelperForDecisions = true }),
			NullLogger<OperatorAiDecisionHelper>.Instance);
	}

	[Fact]
	public async Task Prepended_single_entity_follow_up_is_not_upgraded_to_broad()
	{
		var ai = new Mock<IAiGrpcService>();
		// "reels all active?" is metrics-positive (the "reel" keyword) and would otherwise be 3B-upgraded → dump.
		(await EnabledYesHelper(ai).IsBroadOverviewAsync("reels all active?")).Should().BeFalse();
	}

	[Fact]
	public async Task Plain_single_entity_question_is_not_upgraded()
	{
		var ai = new Mock<IAiGrpcService>();
		(await EnabledYesHelper(ai).IsBroadOverviewAsync("how many reels right now")).Should().BeFalse();
	}

	[Fact]
	public async Task Explicit_broad_keyword_still_wins_not_downgraded()
	{
		var ai = new Mock<IAiGrpcService>();
		// Deterministic keyword short-circuit runs first — the suppress never downgrades an explicit broad.
		(await EnabledYesHelper(ai).IsBroadOverviewAsync("all stats")).Should().BeTrue();
	}

	[Fact]
	public async Task Entityless_novel_broad_phrasing_is_still_upgraded_by_helper()
	{
		var ai = new Mock<IAiGrpcService>();
		// No explicit keyword, no single entity, but metrics-like → the 3B upgrade path is preserved (recall).
		(await EnabledYesHelper(ai).IsBroadOverviewAsync("give me the whole rundown please")).Should().BeTrue();
	}
}
