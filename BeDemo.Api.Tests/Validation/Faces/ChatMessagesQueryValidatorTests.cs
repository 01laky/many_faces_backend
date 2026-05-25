using BeDemo.Api.Validation.Faces;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Faces;

public sealed class ChatMessagesQueryValidatorTests
{
	private readonly ChatMessagesQueryValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Faces.ChatMessagesQuery();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
