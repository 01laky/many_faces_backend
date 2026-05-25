using BeDemo.Api.Configuration;
using BeDemo.Api.Models.Requests.OperatorAi;
using FluentValidation;

namespace BeDemo.Api.Validation.OperatorAi;

public sealed class UpdateOperatorAiLiveStatsCacheSettingsValidator
	: AbstractValidator<UpdateOperatorAiLiveStatsCacheSettingsRequest>
{
	public UpdateOperatorAiLiveStatsCacheSettingsValidator()
	{
		RuleFor(x => x.TtlMilliseconds)
			.InclusiveBetween(
				OperatorAiLiveStatsCacheConstraints.MinTtlMilliseconds,
				OperatorAiLiveStatsCacheConstraints.MaxTtlMilliseconds);
	}
}
