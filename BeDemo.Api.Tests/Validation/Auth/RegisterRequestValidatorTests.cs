using BeDemo.Api.Validation.Auth;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Auth;

public sealed class RegisterRequestValidatorTests
{
	private readonly RegisterRequestValidator _sut = new();

	[Fact]
	public void Valid_register_has_no_errors()
	{
		_sut.TestValidate(new RegisterModel
		{
			Email = "a@b.com",
			Password = "Test1234!@##",
			FirstName = "A",
			LastName = "B",
		}).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Invalid_email_fails()
	{
		_sut.TestValidate(new RegisterModel { Email = "bad", Password = "Test1234!@##" })
			.ShouldHaveValidationErrorFor(x => x.Email);
	}
}
