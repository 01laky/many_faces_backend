using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Validation.Faces;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Faces;

public sealed class WallTicketListQueryValidatorTests
{
	private readonly WallTicketListQueryValidator _sut = new();

	[Fact]
	public void Defaults_are_valid()
	{
		_sut.TestValidate(new WallTicketListQuery()).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Invalid_status_fails()
	{
		_sut.TestValidate(new WallTicketListQuery { Status = "open" }).ShouldHaveValidationErrorFor(x => x.Status);
	}

	[Fact]
	public void Invalid_sortBy_fails()
	{
		_sut.TestValidate(new WallTicketListQuery { SortBy = "creatorName", SortDir = "asc" })
			.ShouldHaveValidationErrorFor(x => x.SortBy);
	}

	[Theory]
	[InlineData("createdAt", "desc")]
	[InlineData("title", "asc")]
	public void Whitelisted_sort_pairs_are_valid(string sortBy, string sortDir)
	{
		_sut.TestValidate(new WallTicketListQuery { SortBy = sortBy, SortDir = sortDir })
			.ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Search_over_max_length_fails()
	{
		_sut.TestValidate(new WallTicketListQuery { Search = new string('x', 201) })
			.ShouldHaveValidationErrorFor(x => x.Search);
	}
}
