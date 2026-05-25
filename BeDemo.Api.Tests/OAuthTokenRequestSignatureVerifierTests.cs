using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

/// <summary>
/// Tests for <see cref="OAuthTokenRequestSignatureVerifier"/> using a fixed <see cref="IClock"/> so canonical payloads match.
/// </summary>
public sealed class OAuthTokenRequestSignatureVerifierTests
{
	private sealed class FixedClock : IClock
	{
		public required DateTime UtcNow { get; init; }
	}

	[Fact]
	public void IsSignatureValid_ReturnsTrue_ForValidEs512Signature()
	{
		var fixedUtc = new DateTime(2026, 4, 11, 12, 0, 0, DateTimeKind.Utc);
		var clock = new FixedClock { UtcNow = fixedUtc };
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
		var securityKey = new Microsoft.IdentityModel.Tokens.ECDsaSecurityKey(ecdsa);
		var mockKeys = new Mock<IECDSAKeyService>();
		mockKeys.Setup(k => k.GetValidationKey()).Returns(securityKey);

		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "demo",
			Username = "user@x.com",
			Scope = "openid",
			SignatureAlgorithm = "ES512",
		};
		var payload = OAuthTokenRequestSignatureVerifier.BuildCanonicalSignatureMessage(request, fixedUtc);
		var sig = ecdsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA512);
		request.Signature = Convert.ToBase64String(sig);

		var sut = new OAuthTokenRequestSignatureVerifier(
			mockKeys.Object,
			NullLogger<OAuthTokenRequestSignatureVerifier>.Instance,
			clock);

		sut.IsSignatureValid(request).Should().BeTrue();
	}

	[Fact]
	public void IsSignatureValid_ReturnsFalse_WhenAlgorithmIsNotEs512()
	{
		var clock = new FixedClock { UtcNow = DateTime.UtcNow };
		var mockKeys = new Mock<IECDSAKeyService>();
		var sut = new OAuthTokenRequestSignatureVerifier(
			mockKeys.Object,
			NullLogger<OAuthTokenRequestSignatureVerifier>.Instance,
			clock);

		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			Signature = "AA==",
			SignatureAlgorithm = "RS256",
		};

		sut.IsSignatureValid(request).Should().BeFalse();
	}

	[Fact]
	public void IsSignatureValid_ReturnsFalse_WhenSignatureBase64Invalid()
	{
		var clock = new FixedClock { UtcNow = DateTime.UtcNow };
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
		var mockKeys = new Mock<IECDSAKeyService>();
		mockKeys.Setup(k => k.GetValidationKey()).Returns(new Microsoft.IdentityModel.Tokens.ECDsaSecurityKey(ecdsa));
		var sut = new OAuthTokenRequestSignatureVerifier(
			mockKeys.Object,
			NullLogger<OAuthTokenRequestSignatureVerifier>.Instance,
			clock);

		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			Signature = "not-valid-base64!!!",
			SignatureAlgorithm = "ES512",
		};

		sut.IsSignatureValid(request).Should().BeFalse();
	}

	[Fact]
	public void IsSignatureValid_ReturnsFalse_WhenSignatureBytesDoNotVerify()
	{
		var fixedUtc = new DateTime(2026, 4, 11, 12, 0, 0, DateTimeKind.Utc);
		var clock = new FixedClock { UtcNow = fixedUtc };
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
		var mockKeys = new Mock<IECDSAKeyService>();
		mockKeys.Setup(k => k.GetValidationKey()).Returns(new Microsoft.IdentityModel.Tokens.ECDsaSecurityKey(ecdsa));

		var request = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "demo",
			Username = "user@x.com",
			Scope = "",
			SignatureAlgorithm = "ES512",
			Signature = Convert.ToBase64String(new byte[128]), // wrong signature
		};

		var sut = new OAuthTokenRequestSignatureVerifier(
			mockKeys.Object,
			NullLogger<OAuthTokenRequestSignatureVerifier>.Instance,
			clock);

		sut.IsSignatureValid(request).Should().BeFalse();
	}
}
