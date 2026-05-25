using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Stories.PublishStoryDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class PublishStoryRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.PublishStoryDto>
{
	public PublishStoryRequestValidator()
	{
		RuleFor(x => x.ScheduledPublishAt).Must(d => !d.HasValue || d.Value > DateTime.UtcNow)
			.WithMessage("Scheduled publish must be in the future.").WithErrorCode("val_datetime_future");
	}
}
