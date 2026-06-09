using BeDemo.Api.Configuration;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Configuration;

/// <summary>
/// Tests for the backend-refactor X3 options validators: the shipped defaults are valid (so startup never breaks),
/// and a genuinely-misconfigured value (non-positive bound, out-of-range score/temperature, undersized signing
/// secret) is rejected with a helpful message instead of being silently clamped.
/// </summary>
public sealed class OptionsValidatorsTests
{
	// ── AiServiceOptions ──────────────────────────────────────────────────────
	[Fact]
	public void AiServiceOptions_defaults_are_valid() =>
		new AiServiceOptionsValidator().Validate(null, new AiServiceOptions()).Succeeded.Should().BeTrue();

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void AiServiceOptions_nonpositive_embedding_dim_is_rejected(int dim)
	{
		var r = new AiServiceOptionsValidator().Validate(null, new AiServiceOptions { EmbeddingDim = dim });
		r.Failed.Should().BeTrue();
		r.FailureMessage.Should().Contain("EmbeddingDim");
	}

	// ── OperatorAiOptions ─────────────────────────────────────────────────────
	[Fact]
	public void OperatorAiOptions_defaults_are_valid() =>
		new OperatorAiOptionsValidator().Validate(null, new OperatorAiOptions()).Succeeded.Should().BeTrue();

	[Fact]
	public void OperatorAiOptions_nonpositive_bounds_are_rejected()
	{
		var v = new OperatorAiOptionsValidator();
		v.Validate(null, new OperatorAiOptions { MaxParallelBundleAiCalls = 0 }).Failed.Should().BeTrue();
		v.Validate(null, new OperatorAiOptions { MaxSelectedBundleIndices = 0 }).Failed.Should().BeTrue();
		v.Validate(null, new OperatorAiOptions { OverallTurnBudgetMs = 0 }).Failed.Should().BeTrue();
		v.Validate(null, new OperatorAiOptions { LiveBundleMaxNewTokens = -1 }).Failed.Should().BeTrue();
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.5)]
	public void OperatorAiOptions_routing_score_out_of_0_1_is_rejected(double score) =>
		new OperatorAiOptionsValidator().Validate(null, new OperatorAiOptions { SkillRoutingMinScore = score }).Failed.Should().BeTrue();

	[Theory]
	[InlineData(-0.1)]
	[InlineData(2.5)]
	public void OperatorAiOptions_map_temperature_out_of_0_2_is_rejected(double temp) =>
		new OperatorAiOptionsValidator().Validate(null, new OperatorAiOptions { MapTemperature = temp }).Failed.Should().BeTrue();

	// ── VideoLoungeOptions ────────────────────────────────────────────────────
	[Fact]
	public void VideoLoungeOptions_stub_defaults_are_valid() =>
		new VideoLoungeOptionsValidator().Validate(null, new VideoLoungeOptions()).Succeeded.Should().BeTrue();

	[Fact]
	public void VideoLoungeOptions_nonpositive_ttl_is_rejected() =>
		new VideoLoungeOptionsValidator().Validate(null, new VideoLoungeOptions { TokenTtlMinutes = 0 }).Failed.Should().BeTrue();

	[Fact]
	public void VideoLoungeOptions_real_signing_requires_a_32_byte_secret()
	{
		var v = new VideoLoungeOptionsValidator();
		// real signing with an undersized secret would crash HMAC-SHA256 at runtime → rejected at startup
		v.Validate(null, new VideoLoungeOptions { UseStubTokens = false, ApiKey = "k", ApiSecret = "short" }).Failed.Should().BeTrue();
		// a 32+ byte secret is fine
		v.Validate(null, new VideoLoungeOptions { UseStubTokens = false, ApiKey = "k", ApiSecret = new string('x', 32) }).Succeeded.Should().BeTrue();
		// non-stub but no keys ⇒ the service falls back to a stub at runtime; not a config error
		v.Validate(null, new VideoLoungeOptions { UseStubTokens = false }).Succeeded.Should().BeTrue();
	}
}
