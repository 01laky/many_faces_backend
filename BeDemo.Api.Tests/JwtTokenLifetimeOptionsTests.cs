using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BeDemo.Api.Configuration;

namespace BeDemo.Api.Tests;

/// <summary>SHV2 BE-A2: startup validation and defaults for JWT access-token lifetimes.</summary>
public sealed class JwtTokenLifetimeOptionsTests
{
    [Fact]
    public void RecommendedRememberMeAccessMinutes_is_seven_days()
    {
        JwtTokenLifetimeOptions.RecommendedRememberMeAccessMinutes.Should().Be(10_080);
    }

    [Fact]
    public void ResolveAccessTokenMinutes_selects_session_or_remember_branch()
    {
        var options = new JwtTokenLifetimeOptions
        {
            ExpiresInMinutes = 60,
            ExpiresInMinutesRememberMe = 10_080,
        };
        options.ResolveAccessTokenMinutes(false).Should().Be(60);
        options.ResolveAccessTokenMinutes(true).Should().Be(10_080);
    }

    [Fact]
    public void BeA2_validation_predicate_rejects_legacy_multi_year_remember_me()
    {
        // Mirrors Program.cs AddOptions<JwtTokenLifetimeOptions>().Validate(...) — keeps unit tests independent of host wiring.
        var legacy = new JwtTokenLifetimeOptions
        {
            ExpiresInMinutes = 60,
            ExpiresInMinutesRememberMe = JwtTokenLifetimeOptions.LegacyMisconfiguredRememberMeMinutes,
        };

        var passesBeA2Rule = legacy.ExpiresInMinutes > 0 &&
                             legacy.ExpiresInMinutesRememberMe > 0 &&
                             legacy.ExpiresInMinutesRememberMe <= JwtTokenLifetimeOptions.MaxRememberMeAccessMinutes &&
                             legacy.ExpiresInMinutesRememberMe >= legacy.ExpiresInMinutes;

        passesBeA2Rule.Should().BeFalse();
    }

    [Fact]
    public void Startup_fails_when_legacy_remember_me_minutes_in_config()
    {
        var act = () =>
        {
            using var factory = new LegacyRememberMeJwtFactory();
            factory.CreateClient();
        };

        act.Should().Throw<Exception>();
    }

    /// <summary>Web host with pre-BE-A2 <c>Jwt:ExpiresInMinutesRememberMe</c> — must not start.</summary>
    private sealed class LegacyRememberMeJwtFactory : CustomWebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:ExpiresInMinutes"] = "60",
                    ["Jwt:ExpiresInMinutesRememberMe"] =
                        JwtTokenLifetimeOptions.LegacyMisconfiguredRememberMeMinutes.ToString(),
                });
            });
        }
    }

    [Fact]
    public void ValidateOnStart_accepts_be_a2_policy_values()
    {
        var services = new ServiceCollection();
        services.AddOptions<JwtTokenLifetimeOptions>()
            .Configure(o =>
            {
                o.ExpiresInMinutes = 60;
                o.ExpiresInMinutesRememberMe = JwtTokenLifetimeOptions.RecommendedRememberMeAccessMinutes;
                o.RefreshTokenDaysRememberMe = 90;
            })
            .Validate(
                o => o.ExpiresInMinutesRememberMe <= JwtTokenLifetimeOptions.MaxRememberMeAccessMinutes,
                "test")
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<IOptions<JwtTokenLifetimeOptions>>().Value.ExpiresInMinutesRememberMe
            .Should()
            .Be(10_080);
    }
}
