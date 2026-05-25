using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Blogs;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Blogs.UpdateBlogDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateBlogRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Blogs.UpdateBlogDto>
{
	public UpdateBlogRequestValidator()
	{
		RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);
		RuleFor(x => x.Content).MaximumLength(ValidationConstants.BlogContentMaxLength).When(x => x.Content != null);
	}
}
