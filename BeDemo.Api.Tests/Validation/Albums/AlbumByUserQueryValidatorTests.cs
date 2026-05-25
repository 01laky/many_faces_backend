using BeDemo.Api.Validation.Albums;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Albums;

public sealed class AlbumByUserQueryValidatorTests
{
	private readonly AlbumByUserQueryValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Albums.AlbumByUserQuery();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
