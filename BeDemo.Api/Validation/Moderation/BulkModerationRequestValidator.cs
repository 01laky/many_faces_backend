using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Moderation;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Moderation.BulkModerationRequest"/> (endpoint-schema-validation §12.1).</summary>
public sealed class BulkModerationRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Moderation.BulkModerationRequest>
{
    public BulkModerationRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithErrorCode("val_collection_min")
            .Must(i => i.Count <= ValidationConstants.BulkModerationMaxItems).WithErrorCode("val_collection_max");
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(x => x.ContentId).GreaterThan(0).WithErrorCode("val_content_id_invalid"));
        When(x => x.Action is BulkModerationAction.Reject or BulkModerationAction.Remove, () =>
        {
            RuleFor(x => x.Reason).NotEmpty().WithErrorCode("val_moderation_reason_required");
        });
    }
}
