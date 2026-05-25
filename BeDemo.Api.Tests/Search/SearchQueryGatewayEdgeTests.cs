using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using ManyFaces.Search.V1;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Search;

public sealed class SearchQueryGatewayEdgeTests
{
	/// <summary>GSH1-T-W01 — gateway unavailable when search disabled returns null autocomplete.</summary>
	[Fact]
	public async Task GSH1_T_W01_SearchDisabled_GatewayReturnsNull()
	{
		var opts = Options.Create(new SearchOptions { Enabled = false });
		var gateway = new SearchWorkerGrpcGateway(opts, Microsoft.Extensions.Logging.Abstractions.NullLogger<SearchWorkerGrpcGateway>.Instance);
		gateway.IsAvailable.Should().BeFalse();
		var result = await gateway.AutocompleteAsync(new AutocompleteRequest { Query = "demo" });
		result.Should().BeNull();
	}

	[Fact]
	public void SearchOptions_DefaultReconciliationValues_MatchPrompt()
	{
		var opts = new SearchOptions();
		opts.ReconciliationIntervalHours.Should().Be(6);
		opts.ReconciliationStartupDelaySeconds.Should().Be(30);
		opts.ReconciliationBatchSize.Should().Be(200);
		opts.ReconciliationRunTimeoutMinutes.Should().Be(45);
		opts.OutboxPollIntervalSeconds.Should().Be(5);
		opts.OutboxWarningPendingCount.Should().Be(1000);
	}

	[Fact]
	public void SearchDocumentTypes_All_ContainsElevenEntityTypes()
	{
		SearchDocumentTypes.All.Should().HaveCount(11);
		SearchDocumentTypes.All.Should().Contain(SearchDocumentTypes.WallTicket);
	}

	[Fact]
	public void SearchIndexVisibility_RemovedAlbum_NotIndexable()
	{
		var album = new BeDemo.Api.Models.Album { RemovedAtUtc = DateTime.UtcNow, Title = "x", CreatorId = "c" };
		SearchIndexVisibility.IsAlbumIndexable(album).Should().BeFalse();
	}
}
