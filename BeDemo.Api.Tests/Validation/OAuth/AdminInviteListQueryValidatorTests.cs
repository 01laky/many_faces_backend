using BeDemo.Api.Models.Requests.OAuth;
using BeDemo.Api.Validation.OAuth;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class AdminInviteListQueryValidatorTests
{
    private readonly AdminInviteListQueryValidator _sut = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _sut.TestValidate(new AdminInviteListQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Take_out_of_range_fails()
    {
        _sut.TestValidate(new AdminInviteListQuery { Take = 0 }).ShouldHaveValidationErrorFor(x => x.Take);
    }

    [Fact]
    public void Page_and_pageSize_are_valid()
    {
        _sut.TestValidate(new AdminInviteListQuery { Page = 2, PageSize = 10 }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_status_fails()
    {
        _sut.TestValidate(new AdminInviteListQuery { Status = "open" }).ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Invalid_sortBy_fails()
    {
        _sut.TestValidate(new AdminInviteListQuery { SortBy = "id", SortDir = "asc" })
            .ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Theory]
    [InlineData("email", "asc")]
    [InlineData("expiresAtUtc", "desc")]
    public void Whitelisted_sort_pairs_are_valid(string sortBy, string sortDir)
    {
        _sut.TestValidate(new AdminInviteListQuery { SortBy = sortBy, SortDir = sortDir })
            .ShouldNotHaveAnyValidationErrors();
    }
}
