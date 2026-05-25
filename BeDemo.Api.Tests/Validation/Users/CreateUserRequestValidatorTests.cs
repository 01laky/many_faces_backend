using BeDemo.Api.Validation.Users;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Users;

public sealed class CreateUserRequestValidatorTests
{
	private readonly CreateUserRequestValidator _sut = new();

	[Fact]
	public void Valid_user_has_no_errors()
	{
		_sut.TestValidate(new CreateUserModel
		{
			Email = "a@b.com",
			Password = "Test1234!@##",
		}).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Short_password_fails()
	{
		_sut.TestValidate(new CreateUserModel { Email = "a@b.com", Password = "short" })
			.ShouldHaveValidationErrorFor(x => x.Password);
	}
}
