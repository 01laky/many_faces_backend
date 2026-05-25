using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Pages.UpdatePageModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdatePageRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Pages.UpdatePageModel>
{
	public UpdatePageRequestValidator()
	{
		RuleFor(x => x.GridSchema).MaximumLength(ValidationConstants.GridSchemaMaxLength).When(x => x.GridSchema != null);
	}
}
