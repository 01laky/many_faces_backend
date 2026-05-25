using BeDemo.Api.Validation.Users;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Users;

public sealed class UpdateUserRequestValidatorTests
{
	private readonly UpdateUserRequestValidator _sut = new();

	[Fact]
	public void Empty_update_is_valid()
	{
		_sut.TestValidate(new UpdateUserModel()).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Password_below_identity_minimum_fails()
	{
		_sut.TestValidate(new UpdateUserModel { Password = "short" })
			.ShouldHaveValidationErrorFor(x => x.Password);
	}
}
