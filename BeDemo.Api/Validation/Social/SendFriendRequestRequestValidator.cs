using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Social;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Social.SendFriendRequestDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class SendFriendRequestRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Social.SendFriendRequestDto>
{
	public SendFriendRequestRequestValidator()
	{
		RuleFor(x => x.ReceiverId).NotEmpty();
	}
}
