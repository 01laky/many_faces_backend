using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class ConfidenceRangeRulesTests
{
	private sealed class Model { public double? C { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.C).ConfidenceRangeRule();
	}

	[Fact]
	public void T6_Above_one_fails() => new Validator().TestValidate(new Model { C = 1.1 })
		.ShouldHaveValidationErrorFor(x => x.C);
}
