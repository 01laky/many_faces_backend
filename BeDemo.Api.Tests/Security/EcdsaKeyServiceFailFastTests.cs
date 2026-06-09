using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace BeDemo.Api.Tests.Security;

/// <summary>
/// Regression tests for the backend-refactor §4.3 fix: a configured-but-missing JWT signing PEM must FAIL FAST
/// outside Development (silently using an ephemeral key breaks JWKS stability and token persistence). Development
/// keeps the ephemeral fallback; no configured path is unaffected.
/// </summary>
public sealed class EcdsaKeyServiceFailFastTests
{
	private static IConfiguration Config(string? pemPath)
	{
		var dict = new Dictionary<string, string?>();
		if (pemPath != null)
			dict["Jwt:SigningPemPath"] = pemPath;
		return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
	}

	private static IHostEnvironment Env(string name)
	{
		var m = new Mock<IHostEnvironment>();
		m.SetupGet(e => e.EnvironmentName).Returns(name);
		m.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());
		return m.Object;
	}

	[Theory]
	[InlineData("Production")]
	[InlineData("Staging")]
	[InlineData("Hardened")]
	public void Missing_configured_pem_fails_fast_outside_development(string environment)
	{
		var act = () => new ECDSAKeyService(Config("/nonexistent/path/jwt-signing.pem"), Env(environment));
		act.Should().Throw<InvalidOperationException>().WithMessage("*SigningPemPath*");
	}

	[Fact]
	public void Missing_configured_pem_falls_back_to_ephemeral_in_development()
	{
		var act = () => new ECDSAKeyService(Config("/nonexistent/path/jwt-signing.pem"), Env("Development"));
		act.Should().NotThrow();
	}

	[Fact]
	public void No_configured_pem_uses_ephemeral_without_throwing()
	{
		var act = () => new ECDSAKeyService(Config(null), Env("Production"));
		act.Should().NotThrow();
	}
}
