using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Pages.UpdatePageTypeModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdatePageTypeRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Pages.UpdatePageTypeModel>
{
	public UpdatePageTypeRequestValidator()
	{
		RuleFor(x => x.Index).MaximumLength(50).When(x => x.Index != null);
	}
}
