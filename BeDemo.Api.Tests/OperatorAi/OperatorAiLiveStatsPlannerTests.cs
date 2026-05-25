using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>Covers AI-P* planner parse edge cases from the live stats agent prompt §14.4.</summary>
public sealed class OperatorAiLiveStatsPlannerTests
{
	private const int CatalogLength = OperatorAiEntityBundleCatalog.BundleCount;

	[Fact]
	public void ParseIndices_valid_json()
	{
		var result = OperatorAiLiveStatsPlanner.ParseIndices(
			"""{"indices":[0,7],"reason":"users and friends"}""",
			CatalogLength,
			maxSelected: 4,
			metricsLike: true);
		result.Indices.Should().Equal(0, 7);
	}

	[Fact]
	public void ParseIndices_markdown_fence()
	{
		var result = OperatorAiLiveStatsPlanner.ParseIndices(
			"""
            ```json
            {"indices":[23,54]}
            ```
            """,
			CatalogLength,
			maxSelected: 4,
			metricsLike: true);
		result.Indices.Should().Equal(23, 54);
	}

	[Fact]
	public void ParseIndices_invalid_json_fallback_to_zero()
	{
		var result = OperatorAiLiveStatsPlanner.ParseIndices(
			"not json",
			CatalogLength,
			maxSelected: 4,
			metricsLike: true);
		result.Indices.Should().Equal(0);
	}

	[Fact]
	public void ParseIndices_out_of_range_filtered()
	{
		var result = OperatorAiLiveStatsPlanner.ParseIndices(
			"""{"indices":[0,99,-1,7]}""",
			CatalogLength,
			maxSelected: 4,
			metricsLike: true);
		result.Indices.Should().Equal(0, 7);
	}

	[Fact]
	public void ParseIndices_truncates_to_max_selected()
	{
		var result = OperatorAiLiveStatsPlanner.ParseIndices(
			"""{"indices":[0,1,2,3,4]}""",
			CatalogLength,
			maxSelected: 2,
			metricsLike: true);
		result.Indices.Should().HaveCount(2);
		result.Indices.Should().Equal(0, 1);
	}

	[Fact]
	public void SupplementIndicesFromMessage_adds_users_and_chat_room_indices()
	{
		var result = OperatorAiLiveStatsPlanner.SupplementIndicesFromMessage(
			"Give me info about users and chat rooms",
			[10],
			CatalogLength,
			maxSelected: 4);
		result.Should().Contain(0);
		result.Should().Contain(10);
		result.Should().Contain(42);
	}
}

public sealed class OperatorAiLiveStatsStitchTests
{
	[Fact]
	public void Stitch_single_part()
	{
		var text = OperatorAiLiveStatsStitch.Stitch([
			new OperatorAiLiveStatsStitch.Part(0, "entity.users", "Total users: 42", Failed: false),
		]);
		text.Should().Contain("entity.users");
		text.Should().Contain("42");
	}

	[Fact]
	public void Stitch_failed_part_labeled()
	{
		var text = OperatorAiLiveStatsStitch.Stitch([
			new OperatorAiLiveStatsStitch.Part(0, "entity.users", "ok", Failed: false),
			new OperatorAiLiveStatsStitch.Part(7, "entity.friendRequests", "", Failed: true),
		]);
		text.Should().Contain("entity.friendRequests");
		text.Should().Contain("unavailable");
	}

	[Fact]
	public void Stitch_empty_parts_safe_message()
	{
		OperatorAiLiveStatsStitch.Stitch([]).Should().Contain("No statistics");
	}
}

public sealed class OperatorAiEntityBundleCatalogTests
{
	[Fact]
	public void Catalog_has_61_stable_indices()
	{
		OperatorAiEntityBundleCatalog.BundleCount.Should().Be(61);
		OperatorAiEntityBundleCatalog.CatalogVersion.Should().Be(2);
		var list = OperatorAiEntityBundleCatalog.ListMetadata();
		list.Should().HaveCount(61);
		list.Select(e => e.Index).Should().BeEquivalentTo(Enumerable.Range(0, 61));
		list.Select(e => e.Id).Should().OnlyHaveUniqueItems();
	}
}
