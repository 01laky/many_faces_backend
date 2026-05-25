using BeDemo.Api.Models.Requests.Blogs;
using BeDemo.Api.Validation.Blogs;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Blogs;

public sealed class BlogListQueryValidatorTests
{
	private readonly BlogListQueryValidator _sut = new();

	[Fact]
	public void Valid_with_faceId_has_no_errors()
	{
		var result = _sut.TestValidate(new BlogListQuery { FaceId = 1 });
		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Valid_with_creatorId_only_has_no_errors()
	{
		var result = _sut.TestValidate(new BlogListQuery { CreatorId = "user-1" });
		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Missing_faceId_and_creatorId_fails()
	{
		var result = _sut.Validate(new BlogListQuery());
		result.IsValid.Should().BeFalse();
	}
}
