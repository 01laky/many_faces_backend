using BeDemo.Api.Models.Requests.Common;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Common;

/// <summary>P0 reference — <see cref="PaginationQuery"/> pagination bounds.</summary>
public sealed class PaginationQueryValidator : AbstractValidator<PaginationQuery>
{
	public PaginationQueryValidator()
	{
		this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
	}
}
