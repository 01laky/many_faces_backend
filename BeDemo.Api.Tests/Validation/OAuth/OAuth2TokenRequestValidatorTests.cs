using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Validation.OAuth;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class OAuth2TokenRequestValidatorTests
{
	private readonly OAuth2TokenRequestValidator _sut = new();

	[Fact]
	public void Missing_grant_type_fails()
	{
		_sut.TestValidate(new OAuth2TokenRequest()).ShouldHaveValidationErrorFor(x => x.GrantType);
	}

	[Fact]
	public void Password_grant_requires_username_and_password()
	{
		var req = new OAuth2TokenRequest { GrantType = "password" };
		_sut.TestValidate(req).ShouldHaveValidationErrorFor(x => x.Username);
		_sut.TestValidate(req).ShouldHaveValidationErrorFor(x => x.Password);
	}

	[Fact]
	public void Valid_password_grant_has_no_errors()
	{
		var req = new OAuth2TokenRequest
		{
			GrantType = "password",
			Username = "u@test.com",
			Password = "Test1234!@##",
		};
		_sut.TestValidate(req).ShouldNotHaveAnyValidationErrors();
	}
}
