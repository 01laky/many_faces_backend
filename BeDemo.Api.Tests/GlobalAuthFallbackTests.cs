using System.Net;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// BSH3-A1: global <c>FallbackPolicy = RequireAuthenticatedUser</c> — protected routes return 401 without Bearer token.
/// </summary>
[Trait("Category", "BackendSecurity")]
public sealed class GlobalAuthFallbackTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public GlobalAuthFallbackTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Protected_users_route_without_token_returns_401()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OAuth_jwks_without_token_stays_public()
    {
        using var client = _factory.CreateUnscopedClient();
        using var response = await client.GetAsync("/api/oauth2/jwks");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Localization_bundle_without_token_stays_public()
    {
        using var client = _factory.CreateUnscopedClient();
        using var response = await client.GetAsync("/api/localization/portal");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
