using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Pages.UpsertPageTranslationsRequest"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpsertPageTranslationsRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Pages.UpsertPageTranslationsRequest>
{
	public UpsertPageTranslationsRequestValidator()
	{
		RuleFor(x => x.Translations).NotEmpty().Must(t => t.Count <= ValidationConstants.MaxPageTranslations);
		RuleForEach(x => x.Translations).ChildRules(c =>
		{
			c.RuleFor(t => t.LanguageCode).NotEmpty().MaximumLength(10);
			c.RuleFor(t => t.TranslatedRoute).NotEmpty().MaximumLength(200);
		});
	}
}
