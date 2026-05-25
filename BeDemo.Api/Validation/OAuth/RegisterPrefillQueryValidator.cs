using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.OAuth.RegisterPrefillQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class RegisterPrefillQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.OAuth.RegisterPrefillQuery>
{
	public RegisterPrefillQueryValidator()
	{
		RuleFor(x => x.Hash).NotEmpty().MaximumLength(ValidationConstants.RegistrationHashMaxLength);
	}
}
