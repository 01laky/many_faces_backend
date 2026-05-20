using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.ChatMessagesQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ChatMessagesQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.ChatMessagesQuery>
{
    private static readonly string[] OperatorSortWhitelist = ["id", "sentAt", "senderUserId"];

    public ChatMessagesQueryValidator()
    {
        When(x => x.Page < 1, () =>
        {
            RuleFor(x => x.PageSize).InclusiveBetween(1, ValidationConstants.PageSizeDefaultMax);
        });

        When(x => x.Page >= 1, () =>
        {
            this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
            this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, OperatorSortWhitelist);
            RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
        });
    }
}
