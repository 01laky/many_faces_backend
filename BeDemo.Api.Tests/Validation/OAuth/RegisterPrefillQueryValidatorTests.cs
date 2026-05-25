using FluentValidation.TestHelper;
using BeDemo.Api.Validation.OAuth;

using BeDemo.Api.Models.Requests.OAuth;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class RegisterPrefillQueryValidatorTests
{
	private readonly RegisterPrefillQueryValidator _sut = new();

	[Fact]
	public void Empty_instance_has_validation_errors()
	{
		var model = new RegisterPrefillQuery();
		var result = _sut.TestValidate(model);
		result.ShouldHaveValidationErrors();
	}
}
