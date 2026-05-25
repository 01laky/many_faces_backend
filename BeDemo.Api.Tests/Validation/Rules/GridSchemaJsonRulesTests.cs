using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class GridSchemaJsonRulesTests
{
	private sealed class Model { public string? Grid { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.Grid).GridSchemaJsonRule();
	}

	[Fact]
	public void T4_Over_max_fails() => new Validator().TestValidate(new Model { Grid = new string('x', ValidationConstants.GridSchemaMaxLength + 1) })
		.ShouldHaveValidationErrorFor(x => x.Grid);
}
