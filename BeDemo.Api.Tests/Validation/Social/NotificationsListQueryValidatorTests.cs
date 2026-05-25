using BeDemo.Api.Models.Requests.Social;
using BeDemo.Api.Validation.Social;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Social;

public sealed class NotificationsListQueryValidatorTests
{
	private readonly NotificationsListQueryValidator _sut = new();

	[Fact]
	public void Limit_in_range_is_valid()
	{
		_sut.TestValidate(new NotificationsListQuery { Limit = 50 }).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Limit_zero_fails()
	{
		_sut.TestValidate(new NotificationsListQuery { Limit = 0 }).ShouldHaveValidationErrorFor(x => x.Limit);
	}
}
