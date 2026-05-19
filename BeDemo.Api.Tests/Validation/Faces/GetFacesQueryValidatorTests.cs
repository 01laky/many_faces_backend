using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Validation.Faces;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Faces;

public sealed class GetFacesQueryValidatorTests
{
    private readonly GetFacesQueryValidator _sut = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _sut.TestValidate(new GetFacesQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_out_of_range_fails(int pageSize)
    {
        _sut.TestValidate(new GetFacesQuery { PageSize = pageSize }).ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Invalid_sortBy_fails()
    {
        var result = _sut.TestValidate(new GetFacesQuery { SortBy = "passwordHash", SortDir = "asc" });
        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_without_sortDir_fails()
    {
        _sut.TestValidate(new GetFacesQuery { SortBy = "title" }).ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData("title", "asc")]
    [InlineData("createdAt", "desc")]
    [InlineData("Title", "asc")]
    public void Whitelisted_sort_pairs_are_valid(string sortBy, string sortDir)
    {
        _sut.TestValidate(new GetFacesQuery { SortBy = sortBy, SortDir = sortDir }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Search_with_null_byte_fails()
    {
        _sut.TestValidate(new GetFacesQuery { Search = "face\0name" }).ShouldHaveValidationErrorFor(x => x.Search);
    }

    [Fact]
    public void Invalid_visibility_fails()
    {
        _sut.TestValidate(new GetFacesQuery { Visibility = "not-a-visibility" })
            .ShouldHaveValidationErrorFor(x => x.Visibility);
    }

    [Fact]
    public void Search_over_max_length_fails()
    {
        _sut.TestValidate(new GetFacesQuery { Search = new string('x', 201) })
            .ShouldHaveValidationErrorFor(x => x.Search);
    }
}
