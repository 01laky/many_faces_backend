using FluentValidation.TestHelper;
using BeDemo.Api.Validation.Social;


namespace BeDemo.Api.Tests.Validation.Social;

public sealed class FollowUserRequestValidatorTests
{
	private readonly FollowUserRequestValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new FollowUserDto();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
