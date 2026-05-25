using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Reels.ReelDetailQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ReelDetailQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Reels.ReelDetailQuery>
{
	public ReelDetailQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
	}
}
