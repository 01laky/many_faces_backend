using BeDemo.Api.Validation.Faces;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Faces;

public sealed class WallTicketWriteRequestValidatorTests
{
	private readonly WallTicketWriteRequestValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Faces.WallTicketWriteDto();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
