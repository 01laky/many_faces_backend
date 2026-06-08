using BeDemo.Api.Services.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

/// <summary>
/// Descriptor coverage lint (spec §17.6, RT-18). The RAG index routes operator questions to stat bundles by
/// embedding each bundle's question-space (description + synonyms + sample questions). If a bundle lacks synonyms
/// or sample questions it effectively drops out of routing, so this build-time test guards full coverage of all
/// 61 catalog indices and unique knowledge ids. It asserts the real merged catalog output (what the indexer ships),
/// not just the authoring table.
/// </summary>
public sealed class OperatorAiKnowledgeDescriptorsTests
{
	[Fact]
	public void Catalog_covers_exactly_indices_0_to_60()
	{
		OperatorAiEntityBundleCatalog.BundleCount.Should().Be(61);

		var entries = OperatorAiEntityBundleCatalog.ListMetadata();
		entries.Should().HaveCount(61);
		entries.Select(e => e.Index).Should().BeEquivalentTo(Enumerable.Range(0, 61));
	}

	[Fact]
	public void Every_bundle_has_non_empty_synonyms_and_sample_questions()
	{
		foreach (var entry in OperatorAiEntityBundleCatalog.ListMetadata())
		{
			entry.Synonyms.Should().NotBeNullOrEmpty($"bundle {entry.Index} ({entry.Id}) must have synonyms for routing recall");
			entry.Synonyms.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s), $"bundle {entry.Index} synonyms must be non-blank");

			entry.SampleQuestions.Should().NotBeNullOrEmpty($"bundle {entry.Index} ({entry.Id}) must have sample questions for routing recall");
			entry.SampleQuestions.Should().OnlyContain(q => !string.IsNullOrWhiteSpace(q), $"bundle {entry.Index} sample questions must be non-blank");

			entry.Description.Should().NotBeNullOrWhiteSpace($"bundle {entry.Index} must reuse its PlannerDescription");
		}
	}

	[Fact]
	public void All_knowledge_ids_are_unique_and_well_formed()
	{
		var entries = OperatorAiEntityBundleCatalog.ListMetadata();
		var ids = entries.Select(e => e.KnowledgeId).ToList();

		ids.Should().OnlyHaveUniqueItems("knowledge_id is the stable upsert key");
		ids.Should().HaveCount(61);
		ids.Should().OnlyContain(id => id.StartsWith("bundle:", StringComparison.Ordinal), "v1 stat-bundle knowledge ids are namespaced");
	}

	[Fact]
	public void Authoring_table_matches_the_catalog_range()
	{
		// The single-source authoring table (§17.6) must cover every catalog index and no extras.
		OperatorAiKnowledgeDescriptors.ByIndex.Keys.Should().BeEquivalentTo(Enumerable.Range(0, OperatorAiEntityBundleCatalog.BundleCount));
	}
}
