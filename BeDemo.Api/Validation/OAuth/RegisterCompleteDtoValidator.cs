using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using BeDemo.Api.Validation.Rules;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>POST /api/oauth2/register/complete — <see cref="RegisterCompleteDto"/> (§11.3).</summary>
public sealed class RegisterCompleteDtoValidator : AbstractValidator<RegisterCompleteDto>
{
	public RegisterCompleteDtoValidator(IOptions<RegistrationInviteOptions> inviteOptions)
	{
		var codeLength = inviteOptions.Value.CodeLength;

		RuleFor(x => x.Hash)
			.NotEmpty()
			.MaximumLength(ValidationConstants.RegistrationHashMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");

		RuleFor(x => x.Code)
			.NotEmpty()
			.Length(codeLength)
			.WithMessage($"Verification code must be {codeLength} characters.")
			.WithErrorCode("val_code_length");

		RuleFor(x => x.Password)
			.NotEmpty()
			.MaximumLength(ValidationConstants.PasswordMaxLength)
			.Must(v => !v.Contains('\0')).WithErrorCode("val_null_byte");

		RuleFor(x => x.ClientId).MaximumLength(ValidationConstants.OAuthClientFieldMaxLength);
		RuleFor(x => x.ClientSecret).MaximumLength(ValidationConstants.OAuthClientFieldMaxLength);
	}
}
