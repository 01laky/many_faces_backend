using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Integration tests for <see cref="Controllers.MePushTokenController"/> push registration and logout cleanup.
/// </summary>
public sealed class MePushTokenControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MePushTokenControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    public void Dispose() { }

    [Fact]
    public async Task PushToken_RegisterThenDeleteByInstallation_ShouldReturn204()
    {
        var client = _factory.CreateFaceClient("public");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var installationId = $"itest-{Guid.NewGuid():N}";
        var register = await client.PostAsJsonAsync(
            "/api/me/push-token",
            new
            {
                registrationToken = "fcm-test-token-" + Guid.NewGuid().ToString("N"),
                platform = "android",
                installationId,
            });
        register.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var del = await client.DeleteAsync(
            $"/api/me/push-token?installationId={Uri.EscapeDataString(installationId)}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PushToken_DeleteAll_WhenInstallationIdOmitted_ShouldReturn204()
    {
        var client = _factory.CreateFaceClient("public");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.PostAsJsonAsync(
            "/api/me/push-token",
            new
            {
                registrationToken = "fcm-bulk-del-" + Guid.NewGuid().ToString("N"),
                platform = "ios",
                installationId = "inst-a",
            });
        await client.PostAsJsonAsync(
            "/api/me/push-token",
            new
            {
                registrationToken = "fcm-bulk-del-" + Guid.NewGuid().ToString("N"),
                platform = "ios",
                installationId = "inst-b",
            });

        var del = await client.DeleteAsync("/api/me/push-token");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
