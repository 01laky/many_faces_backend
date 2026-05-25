using BeDemo.Api.Models.Requests.Users;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Users;

/// <summary>GET /api/users list — pagination, optional search, whitelisted column sort (admin UsersTable).</summary>
public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
	private static readonly string[] SortWhitelist = ["id", "email", "firstName", "lastName", "createdAt"];

	public GetUsersQueryValidator()
	{
		this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
		this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);
		RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
	}
}
