using FluentValidation.TestHelper;
using BeDemo.Api.Validation.OAuth;

using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class RegisterResendDtoValidatorTests
{
	private readonly RegisterResendDtoValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new RegisterResendDto();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
