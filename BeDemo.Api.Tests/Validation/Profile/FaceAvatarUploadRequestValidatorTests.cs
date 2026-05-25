using BeDemo.Api.Validation.Profile;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Profile;

public sealed class FaceAvatarUploadRequestValidatorTests
{
	private readonly FaceAvatarUploadRequestValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Profile.FaceAvatarUploadRequest();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
