using BeDemo.Api.Configuration;
using BeDemo.Api.Utils;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Profile;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Profile.UpdateProfileRequest"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Profile.UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x).Must(m =>
                !string.IsNullOrWhiteSpace(m.FirstName) ||
                !string.IsNullOrWhiteSpace(m.LastName) ||
                m.EnableAnimatedGradient.HasValue ||
                m.PreferredUiLanguage != null ||
                m.LastSelectedFaceId.HasValue ||
                m.ClearPreferredUiLanguage ||
                m.ClearLastSelectedFaceId)
            .WithMessage("At least one field is required.");

        RuleFor(x => x.PreferredUiLanguage)
            .Must(lang => lang == null || PortalSupportedUiLanguages.IsAllowed(lang) || lang.Trim().Length == 0)
            .When(x => x.PreferredUiLanguage != null)
            .WithMessage("Unsupported UI language.");

        RuleFor(x => x.LastSelectedFaceId).OptionalPositiveFaceId();
    }
}
