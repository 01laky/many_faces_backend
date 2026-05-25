using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Reels.UpdateReelCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateReelCommentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Reels.UpdateReelCommentDto>
{
	public UpdateReelCommentRequestValidator()
	{
		RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
	}
}
