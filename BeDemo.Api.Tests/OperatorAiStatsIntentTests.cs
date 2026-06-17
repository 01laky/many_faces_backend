using BeDemo.Api.Utils;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiStatsIntentTests
{
	[Theory]
	[InlineData("Koľko máme používateľov?", true)]
	[InlineData("How many users in dashboard?", true)]
	[InlineData("faceWallTicketsByStatus", true)]
	[InlineData("give me results about all informations in system", true)]
	[InlineData("Give me info about users and chat rooms", true)]
	[InlineData("Koľko je hodín?", false)]
	[InlineData("What time is it now?", false)]
	[InlineData("Explain SignalR in our project", false)]
	public void IsMetricsQuestion_classifies_messages(string message, bool expected)
	{
		Assert.Equal(expected, OperatorAiStatsIntent.IsMetricsQuestion(message));
	}

	[Theory]
	[InlineData("give me results about all informations in system", true)]
	[InlineData("full platform overview", true)]
	[InlineData("how many users?", false)]
	// Full-stats fix: the operator phrasings that previously slipped through must now be detected as broad.
	[InlineData("give me full statistics", true)]
	[InlineData("Give me full stats", true)]
	[InlineData("but i need stats about all entities not just users", true)]
	[InlineData("all entities results", true)]
	[InlineData("give me all the stats", true)]
	[InlineData("I need complete statistics please", true)] // embedded mid-sentence
	[InlineData("FULL STATISTICS", true)] // case-insensitive
	[InlineData("úplné štatistiky prosím", true)] // SK with diacritics
	[InlineData("uplne statistiky", true)] // SK without diacritics
	[InlineData("kompletné štatistiky", true)]
	[InlineData("všetky entity v systéme", true)]
	[InlineData("celé štatistiky", true)]
	// Focused near-misses must stay false (no broad keyword present).
	[InlineData("how many albums are pending approval?", false)]
	[InlineData("show me the reel counts", false)]
	[InlineData("user signups this week", false)]
	// operator-ai conversational-context + broad-overview fix (B1) — the deleted free "all "+(system|platform)
	// rule must no longer fire on focused follow-ups that merely contain both words.
	[InlineData("if our reels in system are all active now", false)] // headline reproduction
	[InlineData("all active?", false)]
	[InlineData("are all the reels in the platform active", false)]
	[InlineData("", false)]
	[InlineData("   ", false)]
	public void IsBroadOverviewQuestion_classifies_messages(string message, bool expected)
	{
		Assert.Equal(expected, OperatorAiStatsIntent.IsBroadOverviewQuestion(message));
	}

	// operator-ai conversational-context + broad-overview fix — B1 ripple: IsBroadOverviewQuestion feeds
	// IsMetricsQuestion (returns true on broad) and IsSimpleCountQuestion (returns false on broad), so deleting the
	// free rule changes those for borderline phrases. "all active?" used to be metrics-like ONLY via the broad
	// false-positive; after B1 it is not. An explicit broad keyword still pulls IsMetricsQuestion true.
	[Theory]
	[InlineData("all active?", false)] // the key ripple: metrics-like ONLY via the old broad false-positive ⇒ now false
	[InlineData("if our reels in system are all active now", true)] // still metrics, but via the "reel" keyword (NOT broad)
	[InlineData("all stats", true)]   // explicit broad keyword ⇒ still metrics
	[InlineData("how many reels?", true)] // ordinary metrics keyword path unaffected
	public void IsMetricsQuestion_after_b1_ripple(string message, bool expected)
	{
		Assert.Equal(expected, OperatorAiStatsIntent.IsMetricsQuestion(message));
	}

	[Theory]
	[InlineData("all active?", false)]  // not metrics after B1 ⇒ not a simple count either
	[InlineData("all stats", false)]    // broad is never a simple count
	[InlineData("how many reels?", true)]
	public void IsSimpleCountQuestion_after_b1_ripple(string message, bool expected)
	{
		Assert.Equal(expected, OperatorAiStatsIntent.IsSimpleCountQuestion(message));
	}

	[Theory]
	[InlineData("what time is it now?", true)]
	[InlineData("can you explain SignalR in our project?", true)]
	[InlineData("write code for me", true)]
	[InlineData("ako funguje kód", true)]
	[InlineData("all active?", false)]
	[InlineData("how many reels?", false)]
	[InlineData("", false)]
	public void ContainsNonMetricsKeyword_classifies_messages(string message, bool expected)
	{
		Assert.Equal(expected, OperatorAiStatsIntent.ContainsNonMetricsKeyword(message));
	}
}
