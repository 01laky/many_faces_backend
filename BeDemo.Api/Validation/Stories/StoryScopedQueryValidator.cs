using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Stories.StoryScopedQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class StoryScopedQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.StoryScopedQuery>
{
	public StoryScopedQueryValidator()
	{
		RuleFor(x => x.FaceId).PositiveFaceId();
	}
}
