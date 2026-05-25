using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Validation.OAuth;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class RegisterSignupRequestValidatorTests
{
	private readonly RegisterSignupRequestValidator _sut = new();

	[Fact]
	public void Valid_email_has_no_errors()
	{
		_sut.TestValidate(new RegisterRequestDto { Email = "a@b.com" })
			.ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Invalid_email_fails()
	{
		_sut.TestValidate(new RegisterRequestDto { Email = "not-email" })
			.ShouldHaveValidationErrorFor(x => x.Email);
	}
}
