using BeDemo.Api.Validation.Rules;
using FluentAssertions;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class SortRulesTests
{
	[Fact]
	public void Empty_sortBy_is_whitelisted()
	{
		SortRules.IsWhitelistedSortBy(null, ["email"]).Should().BeTrue();
	}

	[Theory]
	[InlineData("email")]
	[InlineData("Email")]
	public void Whitelisted_sortBy_is_valid(string sortBy)
	{
		SortRules.IsWhitelistedSortBy(sortBy, ["email"]).Should().BeTrue();
	}

	[Fact]
	public void Unknown_sortBy_is_invalid()
	{
		SortRules.IsWhitelistedSortBy("passwordHash", ["email"]).Should().BeFalse();
	}

	[Theory]
	[InlineData("email.asc")]
	[InlineData("email asc")]
	public void Unsafe_sortBy_token_is_invalid(string sortBy)
	{
		SortRules.IsSafeSortByToken(sortBy).Should().BeFalse();
	}

	[Theory]
	[InlineData("asc")]
	[InlineData("desc")]
	[InlineData("DESC")]
	public void Valid_sort_directions(string dir)
	{
		SortRules.IsValidSortDirection(dir).Should().BeTrue();
	}

	[Fact]
	public void Invalid_sort_direction()
	{
		SortRules.IsValidSortDirection("up").Should().BeFalse();
	}
}
