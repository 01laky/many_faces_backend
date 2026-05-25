using FluentValidation.TestHelper;
using BeDemo.Api.Validation.Users;

using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests.Validation.Users;

public sealed class RegisterPushTokenRequestValidatorTests
{
	private readonly RegisterPushTokenRequestValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new RegisterPushTokenRequestDto();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
