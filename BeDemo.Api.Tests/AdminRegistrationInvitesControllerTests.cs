using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public sealed class AdminRegistrationInvitesControllerTests
    : IClassFixture<RegistrationInviteWebApplicationFactory>, IDisposable
{
    private readonly RegistrationInviteWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminRegistrationInvitesControllerTests(RegistrationInviteWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateUnscopedClient();
    }

    [Fact]
    public async Task List_ShouldReturn403_WhenNotSuperAdmin()
    {
        var email = $"member_{Guid.NewGuid():N}@test.com";
        await IntegrationTestRegistration.CompleteRegistrationAsync(_client, _factory, email, "Test123!@#");

        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = "Test123!@#",
        };
        var tokenResponse = await _client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<OAuth2TokenResponse>();

        using var adminClient = _factory.CreateFaceClient("admin");
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var response = await adminClient.GetAsync("/api/admin/registration-invites");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ShouldReturnOk_WhenSuperAdmin()
    {
        using var admin = _factory.CreateFaceClient("admin");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin));

        var response = await admin.GetAsync("/api/admin/registration-invites");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public void Dispose() => _client.Dispose();
}
