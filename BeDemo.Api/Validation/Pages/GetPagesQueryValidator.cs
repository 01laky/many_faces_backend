using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Pages.GetPagesQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class GetPagesQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Pages.GetPagesQuery>
{
	private static readonly string[] SortWhitelist = ["id", "name", "path", "index", "createdAt", "updatedAt"];

	public GetPagesQueryValidator()
	{
		RuleFor(x => x.FaceId).OptionalPositiveFaceId();
		this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
		this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);
		RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
	}
}
