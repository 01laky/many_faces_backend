using FluentValidation.TestHelper;
using BeDemo.Api.Validation.Albums;


namespace BeDemo.Api.Tests.Validation.Albums;

public sealed class CreateAlbumRequestValidatorTests
{
	private readonly CreateAlbumRequestValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new CreateAlbumDto();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
