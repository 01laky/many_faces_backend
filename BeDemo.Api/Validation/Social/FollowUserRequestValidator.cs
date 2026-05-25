using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Social;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Social.FollowUserDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class FollowUserRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Social.FollowUserDto>
{
	public FollowUserRequestValidator()
	{
		RuleFor(x => x.FollowedId).NotEmpty();
	}
}
