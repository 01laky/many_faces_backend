using FluentValidation.TestHelper;
using BeDemo.Api.Validation.Reels;


namespace BeDemo.Api.Tests.Validation.Reels;

public sealed class CreateReelRequestValidatorTests
{
	private readonly CreateReelRequestValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new CreateReelDto();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
