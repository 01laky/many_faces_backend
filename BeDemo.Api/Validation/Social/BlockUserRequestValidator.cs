using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Social;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Social.BlockUserDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class BlockUserRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Social.BlockUserDto>
{
	public BlockUserRequestValidator()
	{
		RuleFor(x => x.BlockedId).NotEmpty();
	}
}
