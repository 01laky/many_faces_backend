using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Blogs;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Blogs.UpdateBlogCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateBlogCommentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Blogs.UpdateBlogCommentDto>
{
	public UpdateBlogCommentRequestValidator()
	{
		RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
	}
}
