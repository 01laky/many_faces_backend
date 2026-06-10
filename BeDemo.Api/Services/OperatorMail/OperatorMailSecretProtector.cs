using Microsoft.AspNetCore.DataProtection;

namespace BeDemo.Api.Services.OperatorMail;

public interface IOperatorMailSecretProtector
{
	string Protect(string plaintext);

	string Unprotect(string ciphertext);
}

/// <summary>
/// Encrypts operator mail secrets at rest using ASP.NET Data Protection. Logic lives in
/// <see cref="OperatorSecretProtectorBase"/>; this type only fixes the (unchanged) mail purpose string.
/// </summary>
public sealed class OperatorMailSecretProtector : OperatorSecretProtectorBase, IOperatorMailSecretProtector
{
	public OperatorMailSecretProtector(IDataProtectionProvider provider)
		: base(provider, "BeDemo.Api.OperatorMailSystemSettings.v1")
	{
	}
}
