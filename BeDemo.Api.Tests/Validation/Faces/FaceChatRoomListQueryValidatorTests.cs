using BeDemo.Api.Validation.Faces;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Faces;

public sealed class FaceChatRoomListQueryValidatorTests
{
	private readonly FaceChatRoomListQueryValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Faces.FaceChatRoomListQuery();
		var result = _sut.TestValidate(model);
		_ = result;
	}
}
