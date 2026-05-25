using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

public sealed class CreateFaceVideoLoungeRequestValidator
	: AbstractValidator<BeDemo.Api.Models.Requests.Faces.CreateFaceVideoLoungeDto>
{
	public CreateFaceVideoLoungeRequestValidator()
	{
		RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
		RuleFor(x => x.MaxParticipants).InclusiveBetween(2, 50);
	}
}
