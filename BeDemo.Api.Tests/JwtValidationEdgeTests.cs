using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Library-level checks: algorithm confusion (e.g. HS256 token vs ES512-only validator) must fail validation.
/// Mirrors JwtBearer <see cref="Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions"/> ValidAlgorithms policy.
/// </summary>
public sealed class JwtValidationEdgeTests
{
	[Fact]
	public void ValidateToken_RejectsHs256_WhenOnlyEs512Allowed()
	{
		var hmacKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("01234567890123456789012345678901"));
		var signingCreds = new SigningCredentials(hmacKey, SecurityAlgorithms.HmacSha256);
		var handler = new JwtSecurityTokenHandler();
		var jwt = handler.CreateJwtSecurityToken(
			issuer: "BeDemoApi",
			audience: "BeDemoApi",
			subject: new ClaimsIdentity(new[] { new Claim("sub", "x") }),
			notBefore: DateTime.UtcNow.AddMinutes(-1),
			expires: DateTime.UtcNow.AddMinutes(10),
			signingCredentials: signingCreds);

		var token = handler.WriteToken(jwt);

		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
		var esKey = new ECDsaSecurityKey(ecdsa) { KeyId = "es-kid" };

		var parameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKeys = new List<SecurityKey> { esKey },
			ValidateIssuer = true,
			ValidIssuer = "BeDemoApi",
			ValidateAudience = true,
			ValidAudience = "BeDemoApi",
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero,
			ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha512 },
		};

		var act = () => handler.ValidateToken(token, parameters, out _);
		act.Should().Throw<SecurityTokenException>();
	}

	[Fact]
	public void ValidateToken_RejectsExpiredJwt()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
		var esKey = new ECDsaSecurityKey(ecdsa) { KeyId = "kid" };
		var signingCreds = new SigningCredentials(esKey, SecurityAlgorithms.EcdsaSha512);
		var handler = new JwtSecurityTokenHandler();
		var jwt = handler.CreateJwtSecurityToken(
			issuer: "BeDemoApi",
			audience: "BeDemoApi",
			subject: new ClaimsIdentity(new[] { new Claim("sub", "u1") }),
			notBefore: DateTime.UtcNow.AddHours(-2),
			expires: DateTime.UtcNow.AddHours(-1),
			signingCredentials: signingCreds);
		var token = handler.WriteToken(jwt);

		var parameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKeys = new List<SecurityKey> { esKey },
			ValidateIssuer = true,
			ValidIssuer = "BeDemoApi",
			ValidateAudience = true,
			ValidAudience = "BeDemoApi",
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero,
			ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha512 },
		};

		var act = () => handler.ValidateToken(token, parameters, out _);
		act.Should().Throw<SecurityTokenExpiredException>(); // derives from SecurityTokenValidationException, not SecurityTokenException
	}
}
