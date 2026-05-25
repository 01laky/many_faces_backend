using BeDemo.Api.Models.Requests.OAuth;
using BeDemo.Api.Validation.OAuth;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class LocalizationBundleQueryValidatorTests
{
	private readonly LocalizationBundleQueryValidator _sut = new();

	[Fact]
	public void Empty_query_is_valid()
	{
		_sut.TestValidate(new LocalizationBundleQuery()).ShouldNotHaveAnyValidationErrors();
	}

	[Fact]
	public void Version_over_max_length_fails()
	{
		_sut.TestValidate(new LocalizationBundleQuery { V = new string('x', 65) })
			.ShouldHaveValidationErrorFor(x => x.V);
	}
}
