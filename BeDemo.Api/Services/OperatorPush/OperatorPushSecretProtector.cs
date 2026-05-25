using Microsoft.AspNetCore.DataProtection;

namespace BeDemo.Api.Services.OperatorPush;

/// <summary>Encrypts operator push secrets at rest using ASP.NET Data Protection.</summary>
public sealed class OperatorPushSecretProtector : IOperatorPushSecretProtector
{
	private readonly IDataProtector _protector;

	public OperatorPushSecretProtector(IDataProtectionProvider provider)
	{
		_protector = provider.CreateProtector("BeDemo.Api.OperatorPushSystemSettings.v1");
	}

	public string Protect(string plaintext) => _protector.Protect(plaintext);

	public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
