using BeDemo.Api.Validation.Auth;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Auth;

public sealed class LoginRequestValidatorTests
{
	private readonly LoginRequestValidator _sut = new();

	[Fact]
	public void Valid_login_has_no_errors()
	{
		_sut.TestValidate(new LoginModel { Email = "a@b.com", Password = "secret" })
			.ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Missing_email_fails()
	{
		_sut.TestValidate(new LoginModel { Password = "secret" })
			.ShouldHaveValidationErrorFor(x => x.Email);
	}
}
