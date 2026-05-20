using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Validation.Faces;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Faces;

public sealed class FaceProfileCommentsListQueryValidatorTests
{
    private readonly FaceProfileCommentsListQueryValidator _sut = new();

    [Fact]
    public void Portal_defaults_page_zero_are_valid()
    {
        _sut.TestValidate(new FaceProfileCommentsListQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_sortBy_fails_when_paginated()
    {
        _sut.TestValidate(new FaceProfileCommentsListQuery { Page = 1, SortBy = "body", SortDir = "asc" })
            .ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("createdAt", "desc")]
    [InlineData("userId", "asc")]
    public void Whitelisted_sort_pairs_are_valid_when_paginated(string sortBy, string sortDir)
    {
        _sut.TestValidate(new FaceProfileCommentsListQuery { Page = 1, SortBy = sortBy, SortDir = sortDir })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Search_over_max_length_fails_when_paginated()
    {
        _sut.TestValidate(new FaceProfileCommentsListQuery { Page = 1, Search = new string('x', 201) })
            .ShouldHaveValidationErrorFor(x => x.Search);
    }
}
