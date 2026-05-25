using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class EnumDefinedRulesTests
{
	private enum Sample { A = 1 }
	private sealed class Model { public Sample? Value { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Value).EnumDefinedRule<Model, Sample>();
	}

	[Fact]
	public void T7_Invalid_enum_fails() => new Validator().TestValidate(new Model { Value = (Sample)99 })
		.ShouldHaveValidationErrorFor(x => x.Value);
}
