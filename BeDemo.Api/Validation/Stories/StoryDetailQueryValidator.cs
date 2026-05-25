using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Stories.StoryDetailQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class StoryDetailQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.StoryDetailQuery>
{
	public StoryDetailQueryValidator()
	{
		RuleFor(x => x.FaceId).PositiveFaceId();
	}
}
