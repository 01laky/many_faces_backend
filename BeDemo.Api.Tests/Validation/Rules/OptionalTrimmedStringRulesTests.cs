using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class OptionalTrimmedStringRulesTests
{
	private sealed class Model { public string? Note { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Note).OptionalTrimmedString(5);
	}

	[Fact]
	public void T4_Over_max_after_trim_fails() => new Validator().TestValidate(new Model { Note = "  123456  " })
		.ShouldHaveValidationErrorFor(x => x.Note);
}
