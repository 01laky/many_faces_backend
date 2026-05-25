using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class NoNullBytesRulesTests
{
	private sealed class Model { public string? Text { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Text).NoNullBytes();
	}

	[Fact]
	public void T5_Null_byte_fails() => new Validator().TestValidate(new Model { Text = "a\0b" })
		.ShouldHaveValidationErrorFor(x => x.Text);
}
