using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Users;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.DTOs.RegisterPushTokenRequestDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class RegisterPushTokenRequestValidator : AbstractValidator<BeDemo.Api.Models.DTOs.RegisterPushTokenRequestDto>
{
	public RegisterPushTokenRequestValidator()
	{
		RuleFor(x => x.RegistrationToken).NotEmpty().MinimumLength(ValidationConstants.PushTokenMinLength).MaximumLength(ValidationConstants.PushTokenMaxLength); RuleFor(x => x.Platform).PushPlatform();
	}
}
