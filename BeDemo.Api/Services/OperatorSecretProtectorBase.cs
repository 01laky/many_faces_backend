using Microsoft.AspNetCore.DataProtection;

namespace BeDemo.Api.Services;

/// <summary>
/// Shared base for the operator mail/push secret protectors (backend-refactor Phase 2 dedup). Wraps ASP.NET Data
/// Protection with a fixed, per-subsystem <c>purpose</c> so operator secrets are encrypted at rest.
/// <para>
/// The purpose string is a stable cryptographic key-derivation input: each subsystem passes its own historical value
/// verbatim (via its constructor) so previously stored ciphertext stays decryptable. Do not change those strings.
/// </para>
/// </summary>
public abstract class OperatorSecretProtectorBase
{
	private readonly IDataProtector _protector;

	protected OperatorSecretProtectorBase(IDataProtectionProvider provider, string purpose)
	{
		_protector = provider.CreateProtector(purpose);
	}

	public string Protect(string plaintext) => _protector.Protect(plaintext);

	public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
