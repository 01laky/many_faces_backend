using Microsoft.AspNetCore.DataProtection;

namespace BeDemo.Api.Services.OperatorMail;

public interface IOperatorMailSecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}

/// <summary>Encrypts operator mail secrets at rest using ASP.NET Data Protection.</summary>
public sealed class OperatorMailSecretProtector : IOperatorMailSecretProtector
{
    private readonly IDataProtector _protector;

    public OperatorMailSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("BeDemo.Api.OperatorMailSystemSettings.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
