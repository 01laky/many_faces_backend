/*
 * ECDSAKeyService.cs - Service for generating and managing ECDSA keys
 * 
 * This service generates ECDSA (Elliptic Curve Digital Signature Algorithm) keys
 * used for signing and validating JWT tokens.
 * 
 * Uses P-521 curve (NIST P-521), which provides 521-bit security.
 * This is equivalent to approximately 256-bit symmetric security,
 * which is considered very strong encryption.
 * 
 * ES512 algorithm = ECDSA with P-521 curve and SHA-512 hash function
 */

using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace BeDemo.Api.Services;

/// <summary>
/// Interface for ECDSA key service
/// </summary>
public interface IECDSAKeyService
{
    /// <summary>
    /// Gets signing key - used for signing JWT tokens
    /// </summary>
    ECDsaSecurityKey GetSigningKey();

    /// <summary>
    /// Gets validation key - used for validating JWT token signatures
    /// </summary>
    ECDsaSecurityKey GetValidationKey();

    /// <summary>
    /// Gets unique key identifier (for key rotation)
    /// </summary>
    string GetKeyId();
}

/// <summary>
/// ECDSA key service implementation
/// </summary>
public class ECDSAKeyService : IECDSAKeyService
{
    private readonly ECDsaSecurityKey _signingKey;  // ECDSA security key for signing
    private readonly string _keyId;                 // Unique key identifier

    /// <summary>
    /// Constructor - generates new ECDSA key when service is created
    /// </summary>
    public ECDSAKeyService()
    {
        // Creates new ECDSA object with P-521 curve
        // P-521 (NIST P-521) is one of the strongest curves available
        // 521 bits = very high security
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);

        // Creates ECDsaSecurityKey from ECDSA object
        // This key is used in Microsoft.IdentityModel.Tokens for JWT tokens
        _signingKey = new ECDsaSecurityKey(ecdsa)
        {
            // Sets unique key identifier (GUID)
            // This is useful for key rotation - you can have multiple keys and know which one was used
            KeyId = Guid.NewGuid().ToString()
        };

        // Saves KeyId for easy access
        _keyId = _signingKey.KeyId;
    }

    /// <summary>
    /// Returns signing key - used for signing JWT tokens
    /// </summary>
    public ECDsaSecurityKey GetSigningKey() => _signingKey;

    /// <summary>
    /// Returns validation key - used for validating JWT token signatures
    /// 
    /// In this implementation it's the same key as signing key.
    /// In production, different keys could be used (e.g., for key rotation).
    /// </summary>
    public ECDsaSecurityKey GetValidationKey() => _signingKey;

    /// <summary>
    /// Returns unique key identifier
    /// </summary>
    public string GetKeyId() => _keyId;
}
