using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Startup validation for <see cref="AiServiceOptions"/> (backend-refactor X3): bounded numerics must be positive so
/// a misconfiguration fails fast at startup instead of being silently clamped at every call site.
/// </summary>
public sealed class AiServiceOptionsValidator : IValidateOptions<AiServiceOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AiServiceOptions o)
	{
		var errors = new List<string>();
		void Require(bool ok, string field, string detail)
		{
			if (!ok)
				errors.Add($"{AiServiceOptions.SectionName}:{field} {detail}.");
		}

		Require(o.EmbeddingDim > 0, nameof(o.EmbeddingDim), $"must be > 0 (got {o.EmbeddingDim})");
		Require(o.HelperTimeoutMs > 0, nameof(o.HelperTimeoutMs), $"must be > 0 (got {o.HelperTimeoutMs})");
		Require(o.HostProfileStartupTimeoutSeconds >= 1, nameof(o.HostProfileStartupTimeoutSeconds), "must be >= 1");
		Require(o.WarmUpStartupTimeoutSeconds >= 1, nameof(o.WarmUpStartupTimeoutSeconds), "must be >= 1");

		return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
	}
}
