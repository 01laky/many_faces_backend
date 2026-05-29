using BeDemo.Api.Models.Requests.Social;
using BeDemo.Api.Validation.Social;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Social;

public sealed class MessageHistoryQueryValidatorTests
{
	private readonly MessageHistoryQueryValidator _sut = new();

	[Fact]
	public void Limit_in_range_is_valid()
	{
		_sut.TestValidate(new MessageHistoryQuery { Limit = 50 }).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void BeforeId_zero_fails()
	{
		_sut.TestValidate(new MessageHistoryQuery { Limit = 50, BeforeId = 0 })
			.ShouldHaveValidationErrorFor(x => x.BeforeId);
	}

	[Fact]
	public void BeforeId_positive_is_valid()
	{
		_sut.TestValidate(new MessageHistoryQuery { Limit = 50, BeforeId = 100 })
			.ShouldNotHaveValidationErrorFor(x => x.BeforeId);
	}
}
