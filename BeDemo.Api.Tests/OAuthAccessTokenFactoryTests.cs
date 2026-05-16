using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;
using BeDemo.Api.Services;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for <see cref="OAuthAccessTokenFactory"/> (ES512 access JWT + misuse-as-refresh detection).
/// </summary>
public sealed class OAuthAccessTokenFactoryTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"oauth_access_factory_{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (ApplicationDbContext db, ApplicationUser user, Mock<IECDSAKeyService> keys, IConfiguration config) CreateSutContext()
    {
        var db = CreateDb();
        var role = new UserRole
        {
            Name = UserRole.GlobalRoleNames.User,
            Description = "Test",
            Scope = RoleScope.Global,
            CreatedAt = DateTime.UtcNow,
        };
        db.UserRoles.Add(role);
        db.SaveChanges();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = "tok@test.com",
            Email = "tok@test.com",
            EmailConfirmed = true,
            UserRoleId = role.Id,
            AccessTokenVersion = 9,
            FirstName = "T",
            LastName = "U",
        };
        db.Users.Add(user);
        db.SaveChanges();

        // Do not dispose: ECDsaSecurityKey holds the instance for JWT signing/validation during the test.
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP521);
        var esKey = new ECDsaSecurityKey(ecdsa) { KeyId = "unit-kid" };
        var mockKeys = new Mock<IECDSAKeyService>();
        mockKeys.Setup(k => k.GetSigningKey()).Returns(esKey);
        mockKeys.Setup(k => k.GetIssuerSigningKeys()).Returns(new List<SecurityKey> { esKey }.AsReadOnly());
        mockKeys.Setup(k => k.GetKeyId()).Returns("unit-kid");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" },
            { "Jwt:ExpiresInMinutes", "30" },
            { "Jwt:ExpiresInMinutesRememberMe", "120" },
        }).Build();

        return (db, user, mockKeys, config);
    }

    private static IOptions<JwtTokenLifetimeOptions> JwtOptionsFromConfig(IConfiguration config) =>
        Options.Create(new JwtTokenLifetimeOptions
        {
            ExpiresInMinutes = config.GetValue("Jwt:ExpiresInMinutes", 60),
            ExpiresInMinutesRememberMe = config.GetValue(
                "Jwt:ExpiresInMinutesRememberMe",
                JwtTokenLifetimeOptions.RecommendedRememberMeAccessMinutes),
        });

    [Fact]
    public async Task CreateAsync_IncludesRoleAtvAndUsesSessionTtl_WhenRememberMeFalse()
    {
        var (db, user, keys, config) = CreateSutContext();
        await using (db)
        {
            var sut = new OAuthAccessTokenFactory(
                keys.Object,
                config,
                JwtOptionsFromConfig(config),
                db,
                NullLogger<OAuthAccessTokenFactory>.Instance);

            var (jwt, minutes) = await sut.CreateAsync(user, useRememberMeAccessLifetime: false);

            minutes.Should().Be(30);
            var handler = new JwtSecurityTokenHandler();
            var parsed = handler.ReadJwtToken(jwt);
            parsed.Issuer.Should().Be("TestIssuer");
            parsed.Audiences.Should().Contain("TestAudience");
            // JwtSecurityTokenHandler maps claim types to short JWT names (e.g. "role", "given_name").
            parsed.Claims.Should().Contain(c => c.Type == "role" && c.Value == UserRole.GlobalRoleNames.User);
            parsed.Claims.Should().Contain(c => c.Type == BeDemoClaimTypes.AccessTokenVersion && c.Value == "9");
            parsed.Claims.Should().Contain(c => c.Type == "given_name" && c.Value == "T");
        }
    }

    [Fact]
    public async Task CreateAsync_UsesRememberMeTtl_WhenRememberMeTrue()
    {
        var (db, user, keys, config) = CreateSutContext();
        await using (db)
        {
            var sut = new OAuthAccessTokenFactory(
                keys.Object,
                config,
                JwtOptionsFromConfig(config),
                db,
                NullLogger<OAuthAccessTokenFactory>.Instance);

            var (_, minutes) = await sut.CreateAsync(user, useRememberMeAccessLifetime: true);

            minutes.Should().Be(120);
        }
    }

    [Fact]
    public async Task IsValidAccessTokenMisusedAsRefresh_ReturnsTrue_ForFreshAccessJwtFromFactory()
    {
        var (db, user, keys, config) = CreateSutContext();
        await using (db)
        {
            var sut = new OAuthAccessTokenFactory(
                keys.Object,
                config,
                JwtOptionsFromConfig(config),
                db,
                NullLogger<OAuthAccessTokenFactory>.Instance);
            var (access, _) = await sut.CreateAsync(user, false);

            sut.IsValidAccessTokenMisusedAsRefresh(access).Should().BeTrue();
        }
    }

    [Fact]
    public void IsValidAccessTokenMisusedAsRefresh_ReturnsFalse_ForRandomOpaqueString()
    {
        var (db, user, keys, config) = CreateSutContext();
        using (db)
        {
            var sut = new OAuthAccessTokenFactory(
                keys.Object,
                config,
                JwtOptionsFromConfig(config),
                db,
                NullLogger<OAuthAccessTokenFactory>.Instance);

            sut.IsValidAccessTokenMisusedAsRefresh(Convert.ToBase64String(Guid.NewGuid().ToByteArray())).Should().BeFalse();
        }
    }
}
