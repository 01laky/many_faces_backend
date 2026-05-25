using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.OAuth.LocalizationBundleQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class LocalizationBundleQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.OAuth.LocalizationBundleQuery>
{
	public LocalizationBundleQueryValidator()
	{
		RuleFor(x => x.V).MaximumLength(64).When(x => !string.IsNullOrEmpty(x.V));
	}
}
