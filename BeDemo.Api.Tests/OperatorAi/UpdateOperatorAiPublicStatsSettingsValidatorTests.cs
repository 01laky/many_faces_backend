using BeDemo.Api.Configuration;
using BeDemo.Api.Models.Requests.OperatorAi;
using BeDemo.Api.Validation.OperatorAi;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.OperatorAi;

public sealed class UpdateOperatorAiPublicStatsSettingsValidatorTests
{
    private readonly UpdateOperatorAiPublicStatsSettingsValidator _validator = new();

    [Theory]
    [InlineData("off", 1)]
    [InlineData("inline", 2)]
    [InlineData("live", 8)]
    public void Accepts_valid_requests(string mode, int parallel)
    {
        var result = _validator.Validate(new UpdateOperatorAiPublicStatsSettingsRequest
        {
            PublicStatsMode = mode,
            LiveMaxParallelBundleCalls = parallel,
        });
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("banana", 2)]
    [InlineData("inline", 0)]
    [InlineData("live", 9)]
    public void Rejects_invalid_requests(string mode, int parallel)
    {
        var result = _validator.Validate(new UpdateOperatorAiPublicStatsSettingsRequest
        {
            PublicStatsMode = mode,
            LiveMaxParallelBundleCalls = parallel,
        });
        result.IsValid.Should().BeFalse();
    }
}
