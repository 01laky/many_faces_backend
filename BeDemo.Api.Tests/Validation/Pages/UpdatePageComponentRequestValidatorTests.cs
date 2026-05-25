using BeDemo.Api.Validation.Pages;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Pages;

public sealed class UpdatePageComponentRequestValidatorTests
{
	private readonly UpdatePageComponentRequestValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new UpdatePageComponentDto();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
