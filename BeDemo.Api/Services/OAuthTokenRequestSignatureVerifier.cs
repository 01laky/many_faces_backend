using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Services;

/// <summary>
/// Validates optional ECDSA (ES512) request-body signatures for <c>POST /api/oauth2/token</c>.
/// </summary>
/// <remarks>
/// <para>
/// The canonical message includes a wall-clock <c>timestamp</c> field (UTC, second resolution). Callers that sign
/// off-device must use the same instant the server will use when verifying — in practice this path is legacy; the
/// middleware rejects non-empty signature fields (O4) for new clients.
/// </para>
/// <para>
/// <see cref="BuildCanonicalSignatureMessage"/> is <c>internal</c> for unit tests (<c>InternalsVisibleTo</c>) to align signing input with verification.
/// </para>
/// </remarks>
public interface IOAuthTokenRequestSignatureVerifier
{
	/// <summary>Returns <c>true</c> when <paramref name="request"/> carries a valid ES512 signature over the canonical payload.</summary>
	bool IsSignatureValid(OAuth2TokenRequest request);
}

/// <inheritdoc cref="IOAuthTokenRequestSignatureVerifier" />
public sealed class OAuthTokenRequestSignatureVerifier : IOAuthTokenRequestSignatureVerifier
{
	private readonly IECDSAKeyService _keyService;
	private readonly ILogger<OAuthTokenRequestSignatureVerifier> _logger;
	private readonly IClock _clock;

	/// <summary>Creates the verifier using the API signing material as the verification key (legacy model).</summary>
	public OAuthTokenRequestSignatureVerifier(
		IECDSAKeyService keyService,
		ILogger<OAuthTokenRequestSignatureVerifier> logger,
		IClock clock)
	{
		_keyService = keyService;
		_logger = logger;
		_clock = clock;
	}

	/// <inheritdoc />
	public bool IsSignatureValid(OAuth2TokenRequest request)
	{
		if (string.IsNullOrEmpty(request.Signature) || string.IsNullOrEmpty(request.SignatureAlgorithm))
		{
			_logger.LogWarning("Request missing signature or algorithm");
			return false;
		}

		if (request.SignatureAlgorithm != "ES512")
		{
			_logger.LogWarning("Unsupported signature algorithm: {Algorithm}", request.SignatureAlgorithm);
			return false;
		}

		try
		{
			var message = CreateSignatureMessage(request);
			var messageBytes = Encoding.UTF8.GetBytes(message);
			var signatureBytes = Convert.FromBase64String(request.Signature);
			var validationKey = _keyService.GetValidationKey();
			var ecdsa = validationKey.ECDsa ?? throw new InvalidOperationException("ECDSA key not available");
			return ecdsa.VerifyData(messageBytes, signatureBytes, HashAlgorithmName.SHA512);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error validating request signature");
			return false;
		}
	}

	private string CreateSignatureMessage(OAuth2TokenRequest request) =>
		BuildCanonicalSignatureMessage(request, _clock.UtcNow);

	/// <summary>Stable canonical string for ES512 <c>VerifyData</c>; must match signing party input byte-for-byte.</summary>
	/// <param name="request">Token request fields included in the payload (grant type, client id, username, scope).</param>
	/// <param name="utcTimestamp">Instant serialized into the <c>timestamp</c> segment.</param>
	internal static string BuildCanonicalSignatureMessage(OAuth2TokenRequest request, DateTime utcTimestamp)
	{
		var utc = utcTimestamp.Kind == DateTimeKind.Unspecified
			? DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc)
			: utcTimestamp.ToUniversalTime();

		var parts = new List<string>
		{
			$"grant_type={request.GrantType}",
			$"client_id={request.ClientId ?? ""}",
			$"username={request.Username ?? ""}",
			$"scope={request.Scope ?? ""}",
			$"timestamp={utc:yyyy-MM-ddTHH:mm:ssZ}",
		};
		return string.Join("&", parts);
	}
}
