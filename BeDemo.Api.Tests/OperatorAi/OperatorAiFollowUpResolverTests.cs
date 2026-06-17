using BeDemo.Api.Configuration;
using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai conversational-context + broad-overview fix (A1) — the deterministic follow-up resolver: the decision
/// ladder (own-entity → broad → anaphora → else), the per-conversation entity memo, and the bilingual rung-3 gate.
/// The resolver is a pure deterministic component (no model), so these are plain in-process unit tests.
/// </summary>
public sealed class OperatorAiFollowUpResolverTests
{
	private static OperatorAiFollowUpResolver Resolver(bool enabled = true) =>
		new(
			Options.Create(new OperatorAiOptions { FollowUpEntityCarryEnabled = enabled }),
			NullLogger<OperatorAiFollowUpResolver>.Instance);

	[Fact]
	public void Carries_last_entity_onto_anaphoric_follow_up()
	{
		var r = Resolver();
		// Rung 2 — names the entity → unchanged, memo = reels.
		r.Resolve("how many reels are in system right now", 1).Should().Be("how many reels are in system right now");
		// Rung 3 — bare quantifier+status anaphor → prepend the remembered entity.
		r.Resolve("All active?", 1).Should().Be("reels All active?");
	}

	[Fact]
	public void Carries_for_slovak_anaphor_via_status_marker()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 2).Should().Be("how many reels?");
		r.Resolve("a aktívne?", 2).Should().Be("reels a aktívne?");
	}

	[Fact]
	public void Pronoun_anaphor_carries()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 8).Should().Be("how many reels?");
		r.Resolve("those?", 8).Should().Be("reels those?");
	}

	[Fact]
	public void Fresh_conversation_without_memo_passes_through()
	{
		Resolver().Resolve("all active?", 3).Should().Be("all active?");
	}

	[Fact]
	public void Non_metrics_question_does_not_carry()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 4).Should().Be("how many reels?");
		// "explain signalr" is a NonMetricsKeyword → carry off.
		r.Resolve("can you explain SignalR?", 4).Should().Be("can you explain SignalR?");
	}

	[Fact]
	public void How_to_question_without_marker_does_not_carry()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 5).Should().Be("how many reels?");
		// No referential marker (it is a how-to, not an anaphor) → unchanged, never "reels how do I add a face?".
		r.Resolve("how do I add a face?", 5).Should().Be("how do I add a face?");
	}

	[Fact]
	public void Topic_shift_to_new_entity_replaces_memo()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 6).Should().Be("how many reels?");
		r.Resolve("how many users?", 6).Should().Be("how many users?"); // rung 2 → memo now = users
		r.Resolve("all active?", 6).Should().Be("users all active?");    // carries users, NOT reels
	}

	[Fact]
	public void Over_length_message_does_not_carry()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 7).Should().Be("how many reels?");
		const string longMsg = "are all of the items currently active and pending right now please";
		r.Resolve(longMsg, 7).Should().Be(longMsg);
	}

	[Fact]
	public void Broad_request_never_carries()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 9).Should().Be("how many reels?");
		r.Resolve("give me all data", 9).Should().Be("give me all data"); // rung 1 broad → unchanged
	}

	[Fact]
	public void Disabled_resolver_is_a_no_op()
	{
		var r = Resolver(enabled: false);
		r.Resolve("how many reels?", 10).Should().Be("how many reels?");
		r.Resolve("all active?", 10).Should().Be("all active?"); // no carry when disabled
	}

	[Fact]
	public void Memo_is_per_conversation()
	{
		var r = Resolver();
		r.Resolve("how many reels?", 100).Should().Be("how many reels?");
		// Different conversation has no memo → no carry.
		r.Resolve("all active?", 101).Should().Be("all active?");
	}
}
