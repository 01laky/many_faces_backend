using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Stories.CreateStoryCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateStoryCommentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.CreateStoryCommentDto>
{
	public CreateStoryCommentRequestValidator()
	{
		RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
	}
}
