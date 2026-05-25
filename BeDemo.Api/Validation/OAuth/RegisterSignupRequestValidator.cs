using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>POST /api/oauth2/register/request — <see cref="RegisterRequestDto"/> (§11.3).</summary>
public sealed class RegisterSignupRequestValidator : AbstractValidator<RegisterRequestDto>
{
	public RegisterSignupRequestValidator()
	{
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(ValidationConstants.EmailMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");
		RuleFor(x => x.FirstName).MaximumLength(ValidationConstants.NameMaxLength);
		RuleFor(x => x.LastName).MaximumLength(ValidationConstants.NameMaxLength);
		RuleFor(x => x.Locale).MaximumLength(ValidationConstants.LocaleMaxLength);
		RuleFor(x => x.Platform).RegistrationPlatform();
	}
}
