using BeDemo.Api.Configuration;
using BeDemo.Api.Models.Requests.OperatorAi;
using BeDemo.Api.Validation.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

public sealed class UpdateOperatorAiLiveStatsCacheSettingsValidatorTests
{
	private readonly UpdateOperatorAiLiveStatsCacheSettingsValidator _validator = new();

	[Theory]
	[InlineData(29_999)]
	[InlineData(3_600_001)]
	public void Validate_rejects_out_of_range(long ttlMs)
	{
		var result = _validator.Validate(new UpdateOperatorAiLiveStatsCacheSettingsRequest { TtlMilliseconds = ttlMs });
		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData(30_000)]
	[InlineData(300_000)]
	[InlineData(3_600_000)]
	public void Validate_accepts_in_range(long ttlMs)
	{
		var result = _validator.Validate(new UpdateOperatorAiLiveStatsCacheSettingsRequest { TtlMilliseconds = ttlMs });
		result.IsValid.Should().BeTrue();
	}
}
