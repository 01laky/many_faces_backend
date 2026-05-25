using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Auth;

/// <summary>POST /api/auth/register — <see cref="RegisterModel"/> (§11.1).</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterModel>
{
	public RegisterRequestValidator()
	{
		RuleFor(x => x.Email)
			.NotEmpty()
			.EmailAddress()
			.MaximumLength(ValidationConstants.EmailMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");

		RuleFor(x => x.Password)
			.NotEmpty()
			.MaximumLength(ValidationConstants.PasswordMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");

		RuleFor(x => x.FirstName).MaximumLength(ValidationConstants.NameMaxLength).When(x => x.FirstName != null);
		RuleFor(x => x.LastName).MaximumLength(ValidationConstants.NameMaxLength).When(x => x.LastName != null);
	}
}
