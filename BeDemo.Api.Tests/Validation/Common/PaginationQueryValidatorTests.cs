using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation.Common;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Common;

public sealed class PaginationQueryValidatorTests
{
	private readonly PaginationQueryValidator _sut = new();

	[Fact]
	public void Valid_defaults()
	{
		_sut.TestValidate(new PaginationQuery()).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Page_zero_fails()
	{
		_sut.TestValidate(new PaginationQuery { Page = 0 }).ShouldHaveValidationErrorFor(x => x.Page);
	}
}
