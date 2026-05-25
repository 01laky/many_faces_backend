using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Reels;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Reels;

public sealed class UpdateReelRequestValidatorTests
{
	private readonly UpdateReelRequestValidator _sut = new();

	[Fact]
	public void Empty_patch_is_valid()
	{
		_sut.TestValidate(new UpdateReelDto()).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Title_over_max_length_fails()
	{
		_sut.TestValidate(new UpdateReelDto { Title = new string('x', ValidationConstants.TitleMaxLength + 1) })
			.ShouldHaveValidationErrorFor(x => x.Title);
	}
}
