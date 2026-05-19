using BeDemo.Api.Models.Requests.Users;
using BeDemo.Api.Validation.Users;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Users;

public sealed class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _sut = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _sut.TestValidate(new GetUsersQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_out_of_range_fails(int pageSize)
    {
        _sut.TestValidate(new GetUsersQuery { PageSize = pageSize }).ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Invalid_sortBy_fails()
    {
        _sut.TestValidate(new GetUsersQuery { SortBy = "passwordHash", SortDir = "asc" })
            .ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void SortBy_without_sortDir_fails()
    {
        _sut.TestValidate(new GetUsersQuery { SortBy = "email" }).ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData("email", "asc")]
    [InlineData("createdAt", "desc")]
    [InlineData("Email", "asc")]
    public void Whitelisted_sort_pairs_are_valid(string sortBy, string sortDir)
    {
        _sut.TestValidate(new GetUsersQuery { SortBy = sortBy, SortDir = sortDir }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Search_with_null_byte_fails()
    {
        _sut.TestValidate(new GetUsersQuery { Search = "a\0b" }).ShouldHaveValidationErrorFor(x => x.Search);
    }

    [Fact]
    public void Search_over_max_length_fails()
    {
        _sut.TestValidate(new GetUsersQuery { Search = new string('x', 201) })
            .ShouldHaveValidationErrorFor(x => x.Search);
    }
}
