using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Stories.StoryViewQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class StoryViewQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.StoryViewQuery>
{
	public StoryViewQueryValidator()
	{
		RuleFor(x => x.FaceId).PositiveFaceId();
	}
}
