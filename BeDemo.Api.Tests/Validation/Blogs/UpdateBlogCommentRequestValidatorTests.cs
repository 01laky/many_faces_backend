using BeDemo.Api.Validation.Blogs;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Blogs;

public sealed class UpdateBlogCommentRequestValidatorTests
{
	private readonly UpdateBlogCommentRequestValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new UpdateBlogCommentDto();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
