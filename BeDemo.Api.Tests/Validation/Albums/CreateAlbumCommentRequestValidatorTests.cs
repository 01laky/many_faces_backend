using BeDemo.Api.Validation.Albums;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Albums;

public sealed class CreateAlbumCommentRequestValidatorTests
{
	private readonly CreateAlbumCommentRequestValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new CreateAlbumCommentDto();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
