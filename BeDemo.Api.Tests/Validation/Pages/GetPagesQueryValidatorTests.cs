using BeDemo.Api.Models.Requests.Pages;
using BeDemo.Api.Validation.Pages;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Pages;

public sealed class GetPagesQueryValidatorTests
{
    private readonly GetPagesQueryValidator _sut = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _sut.TestValidate(new GetPagesQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FaceId_zero_fails()
    {
        _sut.TestValidate(new GetPagesQuery { FaceId = 0 }).ShouldHaveValidationErrorFor(x => x.FaceId);
    }

    [Fact]
    public void Invalid_sortBy_fails()
    {
        _sut.TestValidate(new GetPagesQuery { SortBy = "body", SortDir = "asc" })
            .ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("path", "desc")]
    [InlineData("createdAt", "asc")]
    public void Whitelisted_sort_pairs_are_valid(string sortBy, string sortDir)
    {
        _sut.TestValidate(new GetPagesQuery { SortBy = sortBy, SortDir = sortDir }).ShouldNotHaveAnyValidationErrors();
    }
}
