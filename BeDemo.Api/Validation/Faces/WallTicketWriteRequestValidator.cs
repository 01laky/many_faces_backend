using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.WallTicketWriteDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class WallTicketWriteRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.WallTicketWriteDto>
{
	public WallTicketWriteRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
		RuleFor(x => x.Description).NotEmpty().MaximumLength(ValidationConstants.WallTicketDescriptionMaxLength);
	}
}
