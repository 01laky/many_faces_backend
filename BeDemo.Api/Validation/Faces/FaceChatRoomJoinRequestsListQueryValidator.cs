using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

public sealed class FaceChatRoomJoinRequestsListQueryValidator
    : AbstractValidator<BeDemo.Api.Models.Requests.Faces.FaceChatRoomJoinRequestsListQuery>
{
    public FaceChatRoomJoinRequestsListQueryValidator()
    {
        this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
    }
}
