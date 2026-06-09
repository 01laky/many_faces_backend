using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Startup validation for <see cref="OperatorAiOptions"/> (backend-refactor X3). The operator-AI options carry many
/// bounded numerics (parallelism, top-K, token caps, timeouts, 0–1 scores); validating them at startup turns a
/// misconfiguration into a loud boot failure instead of a value that is silently clamped at ~30 call sites.
/// </summary>
public sealed class OperatorAiOptionsValidator : IValidateOptions<OperatorAiOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OperatorAiOptions o)
	{
		var errors = new List<string>();
		void Require(bool ok, string field, string detail)
		{
			if (!ok)
				errors.Add($"{OperatorAiOptions.SectionName}:{field} {detail}.");
		}

		Require(o.MaxParallelBundleAiCalls >= 1, nameof(o.MaxParallelBundleAiCalls), "must be >= 1");
		Require(o.MaxSelectedBundleIndices >= 1, nameof(o.MaxSelectedBundleIndices), "must be >= 1");
		Require(o.LiveBundleMaxNewTokens >= 1, nameof(o.LiveBundleMaxNewTokens), "must be >= 1");
		Require(o.LiveStitchMaxNewTokens >= 1, nameof(o.LiveStitchMaxNewTokens), "must be >= 1");
		Require(o.MaxNewTokens >= 1, nameof(o.MaxNewTokens), "must be >= 1");
		Require(o.MaxMessageLength >= 1, nameof(o.MaxMessageLength), "must be >= 1");
		Require(o.EmbedTimeoutMs >= 1, nameof(o.EmbedTimeoutMs), "must be >= 1");
		Require(o.RetrievalTimeoutMs >= 1, nameof(o.RetrievalTimeoutMs), "must be >= 1");
		Require(o.PerBundleGenerateTimeoutMs >= 1, nameof(o.PerBundleGenerateTimeoutMs), "must be >= 1");
		Require(o.OverallTurnBudgetMs >= 1, nameof(o.OverallTurnBudgetMs), "must be >= 1");
		Require(o.AnswerCacheTtlSeconds >= 1, nameof(o.AnswerCacheTtlSeconds), "must be >= 1");
		Require(o.QueryEmbeddingCacheTtlSeconds >= 1, nameof(o.QueryEmbeddingCacheTtlSeconds), "must be >= 1");
		Require(o.SkillRoutingMinScore is >= 0 and <= 1, nameof(o.SkillRoutingMinScore), $"must be in [0,1] (got {o.SkillRoutingMinScore})");
		Require(o.MapTemperature is >= 0 and <= 2, nameof(o.MapTemperature), $"must be in [0,2] (got {o.MapTemperature})");

		return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
	}
}
