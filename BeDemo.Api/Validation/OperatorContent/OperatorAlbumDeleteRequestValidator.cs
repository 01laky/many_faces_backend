using BeDemo.Api.Models.Requests.OperatorContent;
using FluentValidation;

namespace BeDemo.Api.Validation.OperatorContent;

public sealed class OperatorAlbumDeleteRequestValidator : AbstractValidator<OperatorAlbumDeleteRequest>
{
	public OperatorAlbumDeleteRequestValidator()
	{
		// faceId must match album face scope on the detail page.
		RuleFor(x => x.FaceId).GreaterThan(0);
		RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(2000);
		RuleFor(x => x.UserMessage).NotEmpty().MinimumLength(10).MaximumLength(2000);
	}
}
