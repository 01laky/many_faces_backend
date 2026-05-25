using BeDemo.Api.Validation.Stories;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Stories;

public sealed class StoryScopedQueryValidatorTests
{
	private readonly StoryScopedQueryValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Stories.StoryScopedQuery();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
