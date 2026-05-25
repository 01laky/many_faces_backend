using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class UtcRangeRulesTests
{
	private sealed class Model
	{
		public DateTime From { get; set; }
		public DateTime To { get; set; }
	}

	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => this.ApplyUtcRangeRules(x => x.From, x => x.To, 10);
	}

	[Fact]
	public void T10_From_after_to_fails()
	{
		var m = new Model { From = DateTime.UtcNow, To = DateTime.UtcNow.AddDays(-1) };
		new Validator().TestValidate(m).ShouldHaveValidationErrorFor(x => x.To);
	}
}
