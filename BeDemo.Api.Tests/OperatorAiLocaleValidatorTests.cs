using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class OperatorAiLocaleValidatorTests
{
	[Theory]
	[InlineData("en")]
	[InlineData("sk")]
	[InlineData("cz")]
	[InlineData("EN")]
	public void TryNormalize_accepts_supported_codes(string input)
	{
		OperatorAiLocaleValidator.TryNormalize(input, out var normalized).Should().BeTrue();
		normalized.Should().Be(input.ToLowerInvariant());
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("de")]
	[InlineData("en-US")]
	public void TryNormalize_rejects_invalid(string? input)
	{
		OperatorAiLocaleValidator.TryNormalize(input, out _).Should().BeFalse();
	}
}
