using BeDemo.Api.Models;
using BeDemo.Api.Models.Requests.Faces;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Faces;

/// <summary>GET /api/faces list — pagination, search, visibility, and whitelisted sort (admin FacesTable).</summary>
public sealed class GetFacesQueryValidator : AbstractValidator<GetFacesQuery>
{
	private static readonly string[] SortWhitelist =
		["id", "index", "title", "isPublic", "createdAt", "updatedAt"];

	public GetFacesQueryValidator()
	{
		this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
		this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);

		RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
		RuleFor(x => x.Visibility)
			.Must(v => string.IsNullOrWhiteSpace(v) || Enum.TryParse<FaceVisibility>(v, true, out _))
			.WithErrorCode("val_enum_invalid");
	}
}
