using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

public sealed class StoryListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.StoryListQuery>
{
    private static readonly string[] SortWhitelist = ["id", "title", "createdAt", "publishedAt", "isPublished"];

    public StoryListQueryValidator()
    {
        RuleFor(x => x.FaceId).OptionalPositiveFaceId();
        RuleFor(x => x.CreatorId)
            .MaximumLength(450)
            .NoNullBytes()
            .When(x => !string.IsNullOrEmpty(x.CreatorId));
        RuleFor(x => x)
            .Must(q => q.FaceId is > 0 || !string.IsNullOrWhiteSpace(q.CreatorId))
            .WithMessage("Either faceId or creatorId is required");
        this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
        this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);
        RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
    }
}
