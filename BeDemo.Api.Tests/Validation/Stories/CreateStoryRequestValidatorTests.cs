using FluentValidation.TestHelper;
using BeDemo.Api.Validation.Stories;


namespace BeDemo.Api.Tests.Validation.Stories;

public sealed class CreateStoryRequestValidatorTests
{
	private readonly CreateStoryRequestValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new CreateStoryDto();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
