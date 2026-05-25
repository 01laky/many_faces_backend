using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

/// <summary>
/// Verifies optional dual-key loading (K4): current + previous PEM paths produce two issuer verification keys.
/// </summary>
public sealed class ECDSAKeyServiceTests
{
	[Fact]
	public void GetIssuerSigningKeys_ReturnsSingleKey_WhenNoPreviousPem()
	{
		var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
		var env = new Mock<IHostEnvironment>();
		env.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());

		var svc = new ECDSAKeyService(config, env.Object);

		svc.GetIssuerSigningKeys().Should().HaveCount(1);
		svc.GetSigningKey().KeyId.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void GetIssuerSigningKeys_ReturnsTwoKeys_WhenPreviousPemFilesExist()
	{
		var tmp = Directory.CreateTempSubdirectory("bedemo_ecdsa_");
		try
		{
			using var cur = ECDsa.Create(ECCurve.NamedCurves.nistP521);
			using var prev = ECDsa.Create(ECCurve.NamedCurves.nistP521);
			File.WriteAllText(Path.Combine(tmp.FullName, "current.pem"), cur.ExportPkcs8PrivateKeyPem());
			File.WriteAllText(Path.Combine(tmp.FullName, "previous.pem"), prev.ExportPkcs8PrivateKeyPem());

			var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jwt:SigningPemPath"] = "current.pem",
				["Jwt:KeyId"] = "kid-current",
				["Jwt:PreviousSigningPemPath"] = "previous.pem",
				["Jwt:PreviousKeyId"] = "kid-previous",
			}).Build();

			var env = new Mock<IHostEnvironment>();
			env.Setup(e => e.ContentRootPath).Returns(tmp.FullName);

			var svc = new ECDSAKeyService(config, env.Object);

			svc.GetIssuerSigningKeys().Should().HaveCount(2);
			svc.GetIssuerSigningKeys().Select(k => k.KeyId).Should().Contain(new[] { "kid-current", "kid-previous" });
		}
		finally
		{
			try
			{
				tmp.Delete(true);
			}
			catch
			{
				/* best-effort cleanup on CI */
			}
		}
	}
}
