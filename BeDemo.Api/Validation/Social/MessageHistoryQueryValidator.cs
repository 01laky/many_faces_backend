using BeDemo.Api.Models.Requests.Social;
using FluentValidation;

namespace BeDemo.Api.Validation.Social;

public sealed class MessageHistoryQueryValidator : AbstractValidator<MessageHistoryQuery>
{
	public MessageHistoryQueryValidator() =>
		RuleFor(x => x.Limit).InclusiveBetween(1, ValidationConstants.MessageLimitMax);
}
