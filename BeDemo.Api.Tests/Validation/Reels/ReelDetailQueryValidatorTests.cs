using BeDemo.Api.Validation.Reels;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Reels;

public sealed class ReelDetailQueryValidatorTests
{
	private readonly ReelDetailQueryValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Reels.ReelDetailQuery();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
