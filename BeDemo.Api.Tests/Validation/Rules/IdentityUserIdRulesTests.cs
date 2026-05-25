using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class IdentityUserIdRulesTests
{
	private sealed class Model { public string Id { get; set; } = ""; }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Id).IdentityUserIdRule();
	}

	[Fact]
	public void T2_Whitespace_fails() => new Validator().TestValidate(new Model { Id = "user id" })
		.ShouldHaveValidationErrorFor(x => x.Id);
}
