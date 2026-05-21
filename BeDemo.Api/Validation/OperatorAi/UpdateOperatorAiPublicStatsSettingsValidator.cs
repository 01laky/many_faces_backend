using BeDemo.Api.Configuration;
using BeDemo.Api.Models.Requests.OperatorAi;
using FluentValidation;

namespace BeDemo.Api.Validation.OperatorAi;

public sealed class UpdateOperatorAiPublicStatsSettingsValidator
    : AbstractValidator<UpdateOperatorAiPublicStatsSettingsRequest>
{
    public UpdateOperatorAiPublicStatsSettingsValidator()
    {
        RuleFor(x => x.PublicStatsMode)
            .Must(mode => OperatorAiPublicStatsConstraints.ValidPublicStatsModes.Contains(mode))
            .WithMessage("PublicStatsMode must be off, inline, or live.");

        RuleFor(x => x.LiveMaxParallelBundleCalls)
            .InclusiveBetween(
                OperatorAiPublicStatsConstraints.MinLiveMaxParallelBundleCalls,
                OperatorAiPublicStatsConstraints.MaxLiveMaxParallelBundleCalls);
    }
}
