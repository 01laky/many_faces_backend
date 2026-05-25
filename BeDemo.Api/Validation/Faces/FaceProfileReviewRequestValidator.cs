using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.FaceProfileReviewDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class FaceProfileReviewRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.FaceProfileReviewDto>
{
	public FaceProfileReviewRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
		RuleFor(x => x.Text).NotEmpty().MaximumLength(ValidationConstants.WallTicketDescriptionMaxLength);
		RuleFor(x => x.Stars).InclusiveBetween(1, 6).When(x => x.Stars.HasValue);
	}
}
