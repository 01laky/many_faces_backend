using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Profile;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Profile.ProfileMeQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ProfileMeQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Profile.ProfileMeQuery>
{
	public ProfileMeQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
	}
}
