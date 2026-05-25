using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Rules;

public sealed class FaceIdRulesTests
{
	private sealed class Model { public int FaceId { get; set; } }
	private sealed class Validator : AbstractValidator<Model>
	{
		public Validator() => RuleFor(x => x.FaceId).PositiveFaceId();
	}

	[Fact]
	public void T6_Zero_fails() => new Validator().TestValidate(new Model { FaceId = 0 })
		.ShouldHaveValidationErrorFor(x => x.FaceId);
}
