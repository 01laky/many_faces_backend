using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Startup validation for <see cref="VideoLoungeOptions"/> (backend-refactor X3). Guards the TTL and the one runtime
/// crash case: if real (non-stub) signing is configured (both API key and secret present), the HMAC-SHA256 secret
/// must be at least 32 bytes — otherwise <c>VideoLoungeTokenService</c> would throw when minting a token.
/// </summary>
public sealed class VideoLoungeOptionsValidator : IValidateOptions<VideoLoungeOptions>
{
	private const int MinHmacSecretBytes = 32;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, VideoLoungeOptions o)
	{
		var errors = new List<string>();
		void Require(bool ok, string field, string detail)
		{
			if (!ok)
				errors.Add($"{VideoLoungeOptions.SectionName}:{field} {detail}.");
		}

		Require(o.TokenTtlMinutes >= 1, nameof(o.TokenTtlMinutes), $"must be >= 1 (got {o.TokenTtlMinutes})");

		var realSigning = !o.UseStubTokens
			&& !string.IsNullOrWhiteSpace(o.ApiKey)
			&& !string.IsNullOrWhiteSpace(o.ApiSecret);
		if (realSigning)
		{
			Require(System.Text.Encoding.UTF8.GetByteCount(o.ApiSecret!) >= MinHmacSecretBytes,
				nameof(o.ApiSecret), $"must be at least {MinHmacSecretBytes} bytes for HMAC-SHA256 signing");
		}

		return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
	}
}
