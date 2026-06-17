using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// operator-ai conversational-context + broad-overview fix — word-boundary entity detection. The synonym map was
/// authored for embeddings, so the critical property is that substring false-matches ("reel" ∈ "freely",
/// "wall" ∈ "wallet", "story" ∈ "history") do NOT register, while real whole-word mentions do.
/// </summary>
public sealed class OperatorAiEntityDetectionTests
{
	[Theory]
	[InlineData("freely given access")]   // must NOT match "reel"
	[InlineData("open the wallet please")] // must NOT match "wall"
	[InlineData("the history of the project")] // must NOT match "story"
	[InlineData("how do I add a face?")]   // "face" (singular) is not a synonym ("faces" is)
	[InlineData("")]
	[InlineData("   ")]
	public void No_false_substring_matches(string message)
	{
		OperatorAiEntityDetection.DetectEntityBundleIndices(message).Should().BeEmpty();
	}

	[Theory]
	[InlineData("how many reels?", "reels")]
	[InlineData("show me the REELS now", "reels")]      // case-insensitive, trailing token
	[InlineData("how many users are registered", "users")]
	public void Single_entity_resolves_primary_synonym(string message, string expected)
	{
		var primary = OperatorAiEntityDetection.SingleEntityPrimarySynonym(message, out var count);
		count.Should().Be(1);
		primary.Should().Be(expected);
	}

	[Fact]
	public void Two_entities_are_not_a_single_entity()
	{
		var primary = OperatorAiEntityDetection.SingleEntityPrimarySynonym("compare users and reels", out var count);
		count.Should().Be(2);
		primary.Should().BeNull();
	}

	[Fact]
	public void Multiword_synonym_matches_only_when_adjacent()
	{
		// "short videos" is a reels synonym; the two words adjacent → match, scattered → no match.
		OperatorAiEntityDetection.DetectEntityBundleIndices("how many short videos").Should().Contain(32);
		OperatorAiEntityDetection.DetectEntityBundleIndices("short films and music videos").Should().NotContain(32);
	}
}
