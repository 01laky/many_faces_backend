using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class PaginationRulesTests
{
	private sealed class Validator : AbstractValidator<PaginationQuery>
	{
		public Validator() => this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
	}

	[Fact]
	public void T6_Page_zero_fails() => new Validator().TestValidate(new PaginationQuery { Page = 0 })
		.ShouldHaveValidationErrorFor(x => x.Page);

	[Fact]
	public void T11_Valid_defaults() => new Validator().TestValidate(new PaginationQuery())
		.ShouldNotHaveAnyValidationErrors();
}
