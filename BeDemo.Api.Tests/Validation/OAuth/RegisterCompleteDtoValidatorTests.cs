using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Validation.OAuth;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class RegisterCompleteDtoValidatorTests
{
	private readonly RegisterCompleteDtoValidator _sut =
		new(Options.Create(new RegistrationInviteOptions { CodeLength = 6 }));

	[Fact]
	public void Valid_complete_has_no_errors()
	{
		_sut.TestValidate(new RegisterCompleteDto
		{
			Hash = "abc",
			Code = "123456",
			Password = "Test1234!@##",
		}).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Wrong_code_length_fails()
	{
		_sut.TestValidate(new RegisterCompleteDto { Hash = "abc", Code = "12", Password = "x" })
			.ShouldHaveValidationErrorFor(x => x.Code);
	}
}
