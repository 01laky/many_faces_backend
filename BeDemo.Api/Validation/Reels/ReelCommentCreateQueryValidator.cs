using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Reels.ReelCommentCreateQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ReelCommentCreateQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Reels.ReelCommentCreateQuery>
{
	public ReelCommentCreateQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
	}
}
