using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Auth;

/// <summary>POST /api/auth/login — <see cref="LoginModel"/> (§11.1).</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginModel>
{
	public LoginRequestValidator()
	{
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(ValidationConstants.EmailMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");
		RuleFor(x => x.Password).NotEmpty().MaximumLength(ValidationConstants.PasswordMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");
	}
}
