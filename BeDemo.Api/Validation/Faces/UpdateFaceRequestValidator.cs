using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.UpdateFaceModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateFaceRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.UpdateFaceModel>
{
	public UpdateFaceRequestValidator()
	{
		RuleFor(x => x.Index).MaximumLength(100).When(x => x.Index != null);
		RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);
	}
}
