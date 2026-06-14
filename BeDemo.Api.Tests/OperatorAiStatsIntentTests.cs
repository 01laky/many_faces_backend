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
	[InlineData("", false)]
	[InlineData("   ", false)]
	public void IsBroadOverviewQuestion_classifies_messages(string message, bool expected)
	{
		Assert.Equal(expected, OperatorAiStatsIntent.IsBroadOverviewQuestion(message));
	}
}
