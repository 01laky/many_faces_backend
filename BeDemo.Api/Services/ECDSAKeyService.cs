/*
 * ECDSAKeyService.cs - JWT signing keys (ES512 / P-521).
 *
 * JWT signing (digital signature) is separate from TLS: see docs/guides/security-crypto-sockets.md.
 * Development: ephemeral in-process key unless Jwt:SigningPemPath points to a PEM file with an EC private key.
 * Production: set Jwt:SigningPemPath (or mount PEM) so tokens survive restarts and JWKS stays stable until rotation.
 *
 * K4 rotation overlap: optional Jwt:PreviousSigningPemPath + Jwt:PreviousKeyId loads a second private key.
 * New access JWTs are signed only with the current key; validators accept signatures from current OR previous
 * until tokens signed with the old key expire.
 */

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace BeDemo.Api.Services;

/// <summary>
/// JWT signing and validation using ECDSA P-521 (ES512), with optional previous key for rotation overlap (K4).
/// </summary>
public interface IECDSAKeyService
{
	/// <summary>Private key used to sign new access JWTs (always the current rotation key).</summary>
	ECDsaSecurityKey GetSigningKey();

	/// <summary>Primary public verification key (same as signing key material for EC).</summary>
	ECDsaSecurityKey GetValidationKey();

	/// <summary>All keys that may verify an access JWT (current + optional previous). Used by JwtBearer and refresh-token misuse checks.</summary>
	IReadOnlyList<SecurityKey> GetIssuerSigningKeys();

	/// <summary><c>kid</c> for the current signing key (JWT header).</summary>
	string GetKeyId();
}

/// <summary>
/// Loads ECDSA key from <c>Jwt:SigningPemPath</c> when the file exists; otherwise generates an ephemeral key (dev default).
/// Optional <c>Jwt:PreviousSigningPemPath</c> adds a second verification-only key for rotation (K4).
/// </summary>
public class ECDSAKeyService : IECDSAKeyService
{
	private readonly ECDsaSecurityKey _signingKey;
	private readonly ECDsaSecurityKey? _previousSigningKey;
	private readonly ReadOnlyCollection<SecurityKey> _issuerSigningKeys;
	private readonly string _keyId;

	/// <summary>
	/// Prefer <paramref name="configuration"/> <c>Jwt:SigningPemPath</c> for non-ephemeral keys. Optional <c>Jwt:KeyId</c> stabilizes <c>kid</c>.
	/// </summary>
	public ECDSAKeyService(IConfiguration configuration, IHostEnvironment environment)
	{
		var pemPath = configuration["Jwt:SigningPemPath"];
		ECDsa ecdsa;
		string keyId;

		if (!string.IsNullOrWhiteSpace(pemPath))
		{
			var fullPath = Path.IsPathRooted(pemPath)
				? pemPath
				: Path.GetFullPath(Path.Combine(environment.ContentRootPath, pemPath.Trim()));
			if (File.Exists(fullPath))
			{
				var pem = File.ReadAllText(fullPath);
				ecdsa = ECDsa.Create();
				ecdsa.ImportFromPem(pem);
				keyId = configuration["Jwt:KeyId"] ?? "bedemo-ecdsa-1";
			}
			else
			{
				ecdsa = CreateEphemeralP521();
				keyId = Guid.NewGuid().ToString();
			}
		}
		else
		{
			ecdsa = CreateEphemeralP521();
			keyId = configuration["Jwt:KeyId"] ?? Guid.NewGuid().ToString();
		}

		_signingKey = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
		_keyId = _signingKey.KeyId!;

		_previousSigningKey = TryLoadPreviousKey(configuration, environment);
		if (_previousSigningKey != null)
		{
			var list = new List<SecurityKey> { _signingKey, _previousSigningKey };
			_issuerSigningKeys = new ReadOnlyCollection<SecurityKey>(list);
		}
		else
		{
			_issuerSigningKeys = new ReadOnlyCollection<SecurityKey>(new[] { (SecurityKey)_signingKey });
		}
	}

	private static ECDsaSecurityKey? TryLoadPreviousKey(IConfiguration configuration, IHostEnvironment environment)
	{
		var prevPath = configuration["Jwt:PreviousSigningPemPath"];
		if (string.IsNullOrWhiteSpace(prevPath))
			return null;

		var fullPath = Path.IsPathRooted(prevPath)
			? prevPath
			: Path.GetFullPath(Path.Combine(environment.ContentRootPath, prevPath.Trim()));
		if (!File.Exists(fullPath))
			return null;

		var pem = File.ReadAllText(fullPath);
		var ecdsa = ECDsa.Create();
		ecdsa.ImportFromPem(pem);
		var kid = configuration["Jwt:PreviousKeyId"] ?? "bedemo-ecdsa-prev";
		return new ECDsaSecurityKey(ecdsa) { KeyId = kid };
	}

	private static ECDsa CreateEphemeralP521() => ECDsa.Create(ECCurve.NamedCurves.nistP521);

	/// <inheritdoc />
	public ECDsaSecurityKey GetSigningKey() => _signingKey;

	/// <inheritdoc />
	public ECDsaSecurityKey GetValidationKey() => _signingKey;

	/// <inheritdoc />
	public IReadOnlyList<SecurityKey> GetIssuerSigningKeys() => _issuerSigningKeys;

	/// <inheritdoc />
	public string GetKeyId() => _keyId;
}
