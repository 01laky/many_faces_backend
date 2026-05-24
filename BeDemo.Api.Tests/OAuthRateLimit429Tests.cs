using System.Net;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// ACL A21: both <c>register/request</c> and <c>token</c> policies return <strong>429</strong> with <strong>Retry-After</strong> after permit burst.
/// One test method + short fixed windows (see <see cref="RateLimitedOAuthWebApplicationFactory"/>) avoids cross-test partition races.
/// </summary>
[Trait("Category", "BackendSecurity")]
public sealed class OAuthRateLimit429Tests : IClassFixture<RateLimitedOAuthWebApplicationFactory>, IDisposable
{
    private readonly RateLimitedOAuthWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OAuthRateLimit429Tests(RateLimitedOAuthWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateUnscopedClient();
    }

    [Fact]
    public async Task Register_burst_then_token_burst_each_return_429_with_retry_after()
    {
        for (var i = 0; i < 2; i++)
        {
            var regEmail = $"rl_reg_{Guid.NewGuid():N}@test.com";
            (await _client.PostAsJsonAsync(
                    "/api/oauth2/register/request",
                    new RegisterRequestDto
                    {
                        Email = regEmail,
                        FirstName = "T",
                        LastName = "U",
                        Locale = "en",
                    }))
                .StatusCode.Should()
                .Be(HttpStatusCode.OK, $"register/request slot {i}");
        }

        var reg429 = await _client.PostAsJsonAsync(
            "/api/oauth2/register/request",
            new RegisterRequestDto
            {
                Email = $"rl_reg_{Guid.NewGuid():N}@test.com",
                FirstName = "T",
                LastName = "U",
                Locale = "en",
            });
        reg429.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        reg429.Headers.RetryAfter.Should().NotBeNull();

        await Task.Delay(TimeSpan.FromSeconds(3.5));

        var email = $"rl_tok_{Guid.NewGuid():N}@test.com";
        await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test1234!@##");
        var req = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = "Test1234!@##",
        };
        (await _client.PostAsJsonAsync("/api/oauth2/token", req)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsJsonAsync("/api/oauth2/token", req)).StatusCode.Should().Be(HttpStatusCode.OK);
        var tok429 = await _client.PostAsJsonAsync("/api/oauth2/token", req);
        tok429.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        tok429.Headers.RetryAfter.Should().NotBeNull();
        tok429.Headers.RetryAfter!.Delta.Should().NotBeNull();
    }

    public void Dispose() => _client.Dispose();
}
