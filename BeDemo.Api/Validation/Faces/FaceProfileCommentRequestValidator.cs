using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.FaceProfileCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class FaceProfileCommentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.FaceProfileCommentDto>
{
	public FaceProfileCommentRequestValidator()
	{
		RuleFor(x => x.Body).NotEmpty().MaximumLength(ValidationConstants.DescriptionLongMaxLength);
	}
}
