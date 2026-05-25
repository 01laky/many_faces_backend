using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

public sealed class FaceVideoLoungeListQueryValidator
	: AbstractValidator<BeDemo.Api.Models.Requests.Faces.FaceVideoLoungeListQuery>
{
	private static readonly string[] SortWhitelist = ["id", "title", "createdAt", "isPublic"];

	public FaceVideoLoungeListQueryValidator()
	{
		this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
		this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);
		RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
	}
}
