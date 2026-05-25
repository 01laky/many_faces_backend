using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Startup validation for <see cref="IdentityPasswordPolicyOptions"/> (SHV2 BE-A3).
/// </summary>
/// <remarks>
/// Blocks accidental deployment of sub-12 minimum outside Development (e.g. copied legacy <c>4</c> in Production JSON).
/// </remarks>
public sealed class IdentityPasswordPolicyValidateOptions : IValidateOptions<IdentityPasswordPolicyOptions>
{
	private readonly IHostEnvironment _environment;

	/// <summary>Creates the validator.</summary>
	public IdentityPasswordPolicyValidateOptions(IHostEnvironment environment) => _environment = environment;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, IdentityPasswordPolicyOptions options)
	{
		if (options.RequiredLength < IdentityPasswordPolicyOptions.LegacyWeakMinimumLength)
		{
			return ValidateOptionsResult.Fail(
				$"{IdentityPasswordPolicyOptions.SectionName}:{nameof(options.RequiredLength)} must be at least " +
				$"{IdentityPasswordPolicyOptions.LegacyWeakMinimumLength}.");
		}

		if (!_environment.IsDevelopment() &&
			options.RequiredLength < IdentityPasswordPolicyOptions.RecommendedMinimumLength)
		{
			return ValidateOptionsResult.Fail(
				$"{IdentityPasswordPolicyOptions.SectionName}:{nameof(options.RequiredLength)} must be ≥ " +
				$"{IdentityPasswordPolicyOptions.RecommendedMinimumLength} outside Development. " +
				$"Legacy value {IdentityPasswordPolicyOptions.LegacyWeakMinimumLength} is allowed only in " +
				"appsettings.Development.json when ASPNETCORE_ENVIRONMENT=Development.");
		}

		return ValidateOptionsResult.Success;
	}
}
