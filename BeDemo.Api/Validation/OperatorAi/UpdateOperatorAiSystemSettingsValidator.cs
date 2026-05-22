using BeDemo.Api.Models.Requests.OperatorAi;
using FluentValidation;

namespace BeDemo.Api.Validation.OperatorAi;

public sealed class UpdateOperatorAiSystemSettingsValidator
    : AbstractValidator<UpdateOperatorAiSystemSettingsRequest>
{
    public UpdateOperatorAiSystemSettingsValidator()
    {
        // bool is always present when bound; explicit rule keeps validator registered for consistency.
        RuleFor(x => x.AiEnabled).NotNull();
    }
}
