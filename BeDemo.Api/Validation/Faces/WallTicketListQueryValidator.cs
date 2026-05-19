using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Faces;

/// <summary>GET wall-tickets list per face — status filter, search, pagination, whitelisted sort.</summary>
public sealed class WallTicketListQueryValidator : AbstractValidator<WallTicketListQuery>
{
    private static readonly string[] SortWhitelist =
        ["id", "title", "status", "createdAt", "likesCount", "commentsCount"];

    private static readonly string[] StatusWhitelist = ["active", "approved", "denied"];

    public WallTicketListQueryValidator()
    {
        this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
        this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);

        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrWhiteSpace(s) || StatusWhitelist.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithErrorCode("val_enum_invalid");

        RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
    }
}
