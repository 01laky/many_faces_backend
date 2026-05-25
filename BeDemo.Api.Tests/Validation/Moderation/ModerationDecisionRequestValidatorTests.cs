using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Moderation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Moderation;

public sealed class ModerationDecisionRequestValidatorTests
{
	private readonly ModerationDecisionRequestValidator _sut = new();

	[Fact]
	public void Valid_minimal_decision_has_no_errors()
	{
		var model = new ModerationDecisionDto("ok", null);
		_sut.TestValidate(model).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Reason_over_max_length_fails()
	{
		var model = new ModerationDecisionDto(new string('x', ValidationConstants.ModerationReasonMaxLength + 1), null);
		_sut.TestValidate(model).ShouldHaveValidationErrorFor(x => x.Reason);
	}
}
