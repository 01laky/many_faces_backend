using BeDemo.Api.Configuration;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP20 edge cases — background worker poll intervals are configurable.</summary>
public sealed class BeRp20WorkerPollEdgeTests
{
	/// <summary>BE-RP20-U1 — Search outbox poll interval defaults and binds from configuration.</summary>
	[Fact]
	public void BE_RP20_U1_SearchOutboxPollInterval_Configurable()
	{
		var defaults = new SearchOptions();
		defaults.OutboxPollIntervalSeconds.Should().Be(5);

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Search:OutboxPollIntervalSeconds"] = "12",
			})
			.Build();

		var bound = config.GetSection(SearchOptions.SectionName).Get<SearchOptions>();
		bound!.OutboxPollIntervalSeconds.Should().Be(12);
	}

	/// <summary>BE-RP20-U2 — Redis job worker poll interval binds from configuration.</summary>
	[Fact]
	public void BE_RP20_U2_RedisJobWorkerPollInterval_Configurable()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["RedisJobWorker:PollMilliseconds"] = "1500",
			})
			.Build();

		var options = new RedisJobWorkerOptions();
		config.GetSection("RedisJobWorker").Bind(options);
		options.PollMilliseconds.Should().Be(1500);
	}
}
