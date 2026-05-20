using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

public sealed class FaceProfileCommentsListQueryValidator
    : AbstractValidator<BeDemo.Api.Models.Requests.Faces.FaceProfileCommentsListQuery>
{
    private static readonly string[] SortWhitelist = ["createdAt", "userId"];

    public FaceProfileCommentsListQueryValidator()
    {
        When(x => x.Page >= 1, () =>
        {
            this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
            this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);
            RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
        });
    }
}
