using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Pages.CreatePageComponentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreatePageComponentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Pages.CreatePageComponentDto>
{
	public CreatePageComponentRequestValidator()
	{
		RuleFor(x => x.PageId).GreaterThan(0);
		RuleFor(x => x.ComponentTypeId).GreaterThan(0);
		RuleFor(x => x.DisplayModeId).GreaterThan(0);
	}
}
