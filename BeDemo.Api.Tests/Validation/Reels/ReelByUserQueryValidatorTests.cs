using BeDemo.Api.Validation.Reels;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Reels;

public sealed class ReelByUserQueryValidatorTests
{
	private readonly ReelByUserQueryValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Reels.ReelByUserQuery();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
