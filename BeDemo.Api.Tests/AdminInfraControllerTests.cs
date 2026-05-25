using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Integration coverage for <see cref="Controllers.AdminInfraController"/> worker-config GET.
/// </summary>
public sealed class AdminInfraControllerTests
    : IClassFixture<CustomWebApplicationFactory<Program>>,
        IClassFixture<MailDisabledWebApplicationFactory>,
        IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly MailDisabledWebApplicationFactory _mailDisabledFactory;

    public AdminInfraControllerTests(
        CustomWebApplicationFactory<Program> factory,
        MailDisabledWebApplicationFactory mailDisabledFactory)
    {
        _factory = factory;
        _mailDisabledFactory = mailDisabledFactory;
    }

    public void Dispose() { }

    [Fact]
    public async Task WorkerConfig_ShouldReturnUnauthorized_OnAdminFace_WithoutJwt()
    {
        var client = _factory.CreateFaceClient("admin");
        var response = await client.GetAsync("/api/admin/infra/worker-config");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WorkerConfig_ShouldReturnForbidden_ForGlobalAdmin()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/infra/worker-config");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WorkerConfig_ShouldReturnOk_WithConfiguredFlags_ForSuperAdmin()
    {
        await IntegrationTestMail.ResetToBootstrapAsync(_factory);
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/infra/worker-config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WorkerConfigResponse>();
        body.Should().NotBeNull();
        body!.Mail.Configured.Should().BeTrue();
        body.Push.Configured.Should().BeTrue();
        body.Push.RegisteredDeviceCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task WorkerConfig_ShouldReportMailNotConfigured_WhenMailDisabled()
    {
        await IntegrationTestMail.ResetToBootstrapAsync(_mailDisabledFactory, forceEnabled: false);
        var client = _mailDisabledFactory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/infra/worker-config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WorkerConfigResponse>();
        body!.Mail.Configured.Should().BeFalse();
    }

    private sealed class WorkerConfigResponse
    {
        public MailConfig Mail { get; init; } = new();
        public PushConfig Push { get; init; } = new();
    }

    private sealed class MailConfig
    {
        public bool Configured { get; init; }
    }

    private sealed class PushConfig
    {
        public bool Configured { get; init; }
        public int RegisteredDeviceCount { get; init; }
    }
}
