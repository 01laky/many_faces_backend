using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.WallTicketCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class WallTicketCommentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.WallTicketCommentDto>
{
	public WallTicketCommentRequestValidator()
	{
		RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.WallTicketCommentMaxLength);
	}
}
