using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.DTOs.RegisterResendDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class RegisterResendDtoValidator : AbstractValidator<BeDemo.Api.Models.DTOs.RegisterResendDto>
{
	public RegisterResendDtoValidator()
	{
		RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(ValidationConstants.EmailMaxLength); RuleFor(x => x.Platform).RegistrationPlatform();
	}
}
