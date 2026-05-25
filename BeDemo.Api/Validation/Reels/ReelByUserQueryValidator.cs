using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Reels.ReelByUserQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ReelByUserQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Reels.ReelByUserQuery>
{
	public ReelByUserQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
	}
}
