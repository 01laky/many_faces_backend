using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class SlugPathRulesTests
{
	private sealed class Model { public string Path { get; set; } = "/ok"; }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Path).SlugPathRule();
	}

	[Fact]
	public void T5_Path_with_dotdot_fails() => new Validator().TestValidate(new Model { Path = "/../secret" })
		.ShouldHaveValidationErrorFor(x => x.Path);
}
