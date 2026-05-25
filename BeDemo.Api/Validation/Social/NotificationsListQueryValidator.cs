using BeDemo.Api.Models.Requests.Social;
using FluentValidation;

namespace BeDemo.Api.Validation.Social;

public sealed class NotificationsListQueryValidator : AbstractValidator<NotificationsListQuery>
{
	public NotificationsListQueryValidator() =>
		RuleFor(x => x.Limit).InclusiveBetween(1, ValidationConstants.NotificationLimitMax);
}
