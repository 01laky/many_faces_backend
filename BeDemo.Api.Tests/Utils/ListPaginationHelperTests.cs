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

	/// <summary>
	/// Exhaustive boundary/degenerate coverage for the clamp math behind the grid-snapshot pagination fix:
	/// out-of-range pages, exact page boundaries, empty/negative totals, and — crucially — pageSize ≤ 0
	/// (which must NOT divide by zero / produce int.MinValue garbage).
	/// </summary>
	[Theory]
	// page in range / at boundaries
	[InlineData(1, 10, 25, 1, 3)] // first page
	[InlineData(2, 10, 25, 2, 3)] // middle page
	[InlineData(3, 10, 25, 3, 3)] // last partial page
	[InlineData(3, 10, 30, 3, 3)] // exact full last page
	[InlineData(4, 10, 30, 3, 3)] // one past the exact last page → clamps to last
	[InlineData(999, 10, 25, 3, 3)] // far out of range → clamps to last
									// page below 1
	[InlineData(0, 10, 25, 1, 3)] // zero page → 1
	[InlineData(-5, 10, 25, 1, 3)] // negative page → 1
	[InlineData(int.MinValue, 10, 25, 1, 3)] // extreme negative page → 1
											 // empty / tiny / non-divisible totals
	[InlineData(1, 10, 0, 1, 1)] // empty result still has 1 page
	[InlineData(7, 10, 0, 1, 1)] // out-of-range page on empty → 1
	[InlineData(1, 10, 1, 1, 1)] // single item
	[InlineData(1, 10, 11, 1, 2)] // 11 items over size 10 → 2 pages
	[InlineData(1, 10, -5, 1, 1)] // negative total guarded → 1 page
								  // pageSize ≤ 0 must be treated as 1 (no divide-by-zero / no garbage)
	[InlineData(1, 0, 25, 1, 25)]
	[InlineData(999, 0, 25, 25, 25)]
	[InlineData(1, -5, 25, 1, 25)]
	[InlineData(1, 0, 0, 1, 1)]
	public void ClampPage_handles_boundaries_and_degenerate_inputs(
		int page, int pageSize, int totalCount, int expectedPage, int expectedTotalPages)
	{
		var (clampedPage, totalPages) = ListPaginationHelper.ClampPage(page, pageSize, totalCount);

		clampedPage.Should().Be(expectedPage);
		totalPages.Should().Be(expectedTotalPages);
		totalPages.Should().BeGreaterThanOrEqualTo(1, "totalPages is never zero/negative garbage");
		clampedPage.Should().BeInRange(1, totalPages, "the clamped page always sits within [1, totalPages]");
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
