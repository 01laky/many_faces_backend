using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests.Utils;

public sealed class ListPaginationHelperTests
{
	[Fact]
	public void ClampPage_clamps_high_page_to_totalPages()
	{
		var (page, totalPages) = ListPaginationHelper.ClampPage(999, 10, 25);
		totalPages.Should().Be(3);
		page.Should().Be(3);
	}

	[Fact]
	public void ClampPage_never_returns_page_below_one()
	{
		var (page, _) = ListPaginationHelper.ClampPage(0, 10, 5);
		page.Should().Be(1);
	}

	[Fact]
	public void BuildEnvelope_exposes_items_and_totals()
	{
		var envelope = ListPaginationHelper.BuildEnvelope(new[] { 1, 2 }, 2, 10, 12, 2);
		envelope.Should().BeEquivalentTo(new
		{
			items = new[] { 1, 2 },
			page = 2,
			pageSize = 10,
			totalCount = 12,
			totalPages = 2,
		});
	}
}
