using BeDemo.Api.Validation.Blogs;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Blogs;

public sealed class UpdateBlogRequestValidatorTests
{
	private readonly UpdateBlogRequestValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new UpdateBlogDto();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
