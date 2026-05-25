using BeDemo.Api.Models.Requests.Stats;
using BeDemo.Api.Validation.Stats;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Stats;

public sealed class StatsTimeseriesQueryValidatorTests
{
	private readonly StatsTimeseriesQueryValidator _sut = new();

	[Fact]
	public void Valid_query_has_no_errors()
	{
		var q = new StatsTimeseriesQuery
		{
			Metric = "users",
			FromUtc = DateTime.UtcNow.AddDays(-7),
			ToUtc = DateTime.UtcNow,
			Bucket = "day",
		};
		_sut.TestValidate(q).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Unknown_metric_fails()
	{
		var q = new StatsTimeseriesQuery
		{
			Metric = "unknown",
			FromUtc = DateTime.UtcNow.AddDays(-1),
			ToUtc = DateTime.UtcNow,
		};
		_sut.TestValidate(q).ShouldHaveValidationErrorFor(x => x.Metric);
	}
}
