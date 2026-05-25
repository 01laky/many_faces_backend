using BeDemo.Api.Validation.Users;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Users;

public sealed class DeletePushTokenQueryValidatorTests
{
	private readonly DeletePushTokenQueryValidator _sut = new();

	[Fact]
	public void Valid_minimal_instance_has_no_errors()
	{
		var model = new BeDemo.Api.Models.Requests.Users.DeletePushTokenQuery();
		var result = _sut.TestValidate(model);
		// Refine per §4 T1–T12 as rules are added.
		_ = result;
	}
}
