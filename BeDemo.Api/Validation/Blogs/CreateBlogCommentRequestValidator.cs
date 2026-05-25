using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Blogs;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Blogs.CreateBlogCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateBlogCommentRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Blogs.CreateBlogCommentDto>
{
	public CreateBlogCommentRequestValidator()
	{
		RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
	}
}
