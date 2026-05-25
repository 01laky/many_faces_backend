using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Stories.StoryMineQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class StoryMineQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.StoryMineQuery>
{
	public StoryMineQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
	}
}
