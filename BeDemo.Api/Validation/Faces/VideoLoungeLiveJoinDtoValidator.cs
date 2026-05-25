using FluentValidation;

namespace BeDemo.Api.Validation.Faces;

public sealed class VideoLoungeLiveJoinDtoValidator
	: AbstractValidator<BeDemo.Api.Models.Requests.Faces.VideoLoungeLiveJoinDto>
{
	public VideoLoungeLiveJoinDtoValidator()
	{
		RuleFor(x => x.JoinMode)
			.NotEmpty()
			.Must(m => BeDemo.Api.Utils.VideoLoungeJoinModeParser.TryParseMemberMode(m, out _))
			.WithMessage("joinMode must be Viewer, Listener, or Full");
	}
}
