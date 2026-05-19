using BeDemo.Api.Models.Requests.OAuth;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>Admin registration-invites list — page/pageSize envelope; legacy skip/take still accepted one release.</summary>
public sealed class AdminInviteListQueryValidator : AbstractValidator<AdminInviteListQuery>
{
    private static readonly string[] SortWhitelist = ["email", "status", "createdAtUtc", "expiresAtUtc"];
    private static readonly string[] StatusWhitelist = ["pending", "completed", "expired", "revoked"];

    public AdminInviteListQueryValidator()
    {
        this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
        this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);

        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0).When(x => x.Skip.HasValue).WithErrorCode("val_skip_min");
        RuleFor(x => x.Take).InclusiveBetween(1, ValidationConstants.PageSizeDefaultMax)
            .When(x => x.Take.HasValue).WithErrorCode("val_take_range");

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || StatusWhitelist.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithErrorCode("val_enum_invalid");

        RuleFor(x => x.EmailContains).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.EmailContains));
    }
}
