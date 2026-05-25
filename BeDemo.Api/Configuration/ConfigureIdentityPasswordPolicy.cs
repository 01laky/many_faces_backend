using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Configuration;

/// <summary>
/// Applies <see cref="IdentityPasswordPolicyOptions.RequiredLength"/> to Identity after options binding (SHV2 BE-A3).
/// </summary>
/// <remarks>
/// Keeps complexity flags in <c>Program.cs</c> while allowing environment-specific minimum length from JSON config.
/// </remarks>
public sealed class ConfigureIdentityPasswordPolicy : IPostConfigureOptions<IdentityOptions>
{
	private readonly IdentityPasswordPolicyOptions _policy;

	/// <summary>Creates the post-configurator.</summary>
	public ConfigureIdentityPasswordPolicy(IOptions<IdentityPasswordPolicyOptions> policy) =>
		_policy = policy.Value;

	/// <inheritdoc />
	public void PostConfigure(string? name, IdentityOptions options) =>
		options.Password.RequiredLength = _policy.RequiredLength;
}
