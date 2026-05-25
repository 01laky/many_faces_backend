using FluentValidation.TestHelper;
using BeDemo.Api.Validation.Blogs;


namespace BeDemo.Api.Tests.Validation.Blogs;

public sealed class CreateBlogCommentRequestValidatorTests
{
	private readonly CreateBlogCommentRequestValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new CreateBlogCommentDto();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
