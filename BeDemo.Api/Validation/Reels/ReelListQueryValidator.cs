using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

public sealed class ReelListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Reels.ReelListQuery>
{
	private static readonly string[] SortWhitelist = ["id", "title", "createdAt", "updatedAt", "approvalStatus"];

	public ReelListQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
		RuleFor(x => x.CreatorId)
			.MaximumLength(450)
			.NoNullBytes()
			.When(x => !string.IsNullOrEmpty(x.CreatorId));
		this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
		this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);
		RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
		RuleFor(x => x.ApprovalStatus)
			.Must(s => string.IsNullOrEmpty(s) || Enum.TryParse<BeDemo.Api.Models.ContentApprovalStatus>(s, true, out _))
			.WithMessage("Invalid approvalStatus")
			.When(x => !string.IsNullOrEmpty(x.ApprovalStatus));
	}
}
