using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Security;
using BeDemo.Api.Utils;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// ACC-B4…B17 — super-admin-only platform bar on admin face prefix (integration edge cases).
/// </summary>
public sealed class PlatformSuperAdminAccessEdgeTests
    : IClassFixture<CustomWebApplicationFactory<Program>>,
        IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public PlatformSuperAdminAccessEdgeTests(CustomWebApplicationFactory<Program> factory) =>
        _factory = factory;

    public void Dispose() { }

    /// <summary>ACC-B4 — global ADMIN JWT on admin face Stats → 403.</summary>
    [Fact]
    public async Task Stats_ReturnsForbidden_ForGlobalAdmin_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Stats");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B5 — SUPER_ADMIN JWT on admin face Stats → 200.</summary>
    [Fact]
    public async Task Stats_ReturnsOk_ForSuperAdmin_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Stats");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>ACC-B6 — ADMIN capabilities on admin face omit platform:super and platform:admin.</summary>
    [Fact]
    public async Task Capabilities_OmitsPlatformSuperAndAdmin_ForGlobalAdmin_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/me/capabilities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var caps = await response.Content.ReadFromJsonAsync<CapabilitiesResponse>();
        caps.Should().NotBeNull();
        caps!.Permissions.Should().NotContain(AclPermissionKeys.PlatformSuper);
        caps.Permissions.Should().NotContain(AclPermissionKeys.PlatformAdmin);
    }

    /// <summary>ACC-B7 — SUPER_ADMIN capabilities include platform:super and platform:admin.</summary>
    [Fact]
    public async Task Capabilities_IncludesPlatformSuperAndAdmin_ForSuperAdmin_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/me/capabilities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var caps = await response.Content.ReadFromJsonAsync<CapabilitiesResponse>();
        caps!.Permissions.Should().Contain(AclPermissionKeys.PlatformSuper);
        caps.Permissions.Should().Contain(AclPermissionKeys.PlatformAdmin);
    }

    /// <summary>ACC-B8 — ADMIN on operator-ai conversations → 403.</summary>
    [Fact]
    public async Task OperatorAiConversations_ReturnsForbidden_ForGlobalAdmin()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/operator-ai/conversations");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B9 — ADMIN on faces list → 403.</summary>
    [Fact]
    public async Task FacesList_ReturnsForbidden_ForGlobalAdmin_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/faces");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B10 — ADMIN on admin-face faces/config → 403.</summary>
    [Fact]
    public async Task FacesConfig_ReturnsForbidden_ForGlobalAdmin_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/faces/config");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B11 — ADMIN on public faces/config still sees multi-face graph (§1.5).</summary>
    [Fact]
    public async Task FacesConfig_ReturnsMultiFaceGraph_ForGlobalAdmin_OnPublicFace()
    {
        var client = _factory.CreateFaceClient("public");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/faces/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var facesConfig = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var indices = facesConfig!.Select(f => f.GetProperty("index").GetString()).ToList();
        indices.Should().Contain("public");
        indices.Should().Contain("basic");
    }

    /// <summary>ACC-B12 — USER JWT on admin face Stats → 403.</summary>
    [Fact]
    public async Task Stats_ReturnsForbidden_ForRegisteredUser_OnAdminFace()
    {
        var oauth = _factory.CreateFaceClient("public");
        var token = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(
            oauth,
            _factory,
            email: $"edge_user_{Guid.NewGuid():N}@test.com");

        var client = _factory.CreateFaceClient("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Stats");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B13 — SUPER_ADMIN on public face Stats operator route → 403 (wrong scope).</summary>
    [Fact]
    public async Task Stats_ReturnsForbidden_ForSuperAdmin_OnPublicFace()
    {
        var client = _factory.CreateFaceClient("public");
        var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/Stats");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B14 — ADMIN cannot create users on admin face.</summary>
    [Fact]
    public async Task CreateUser_ReturnsForbidden_ForGlobalAdmin_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            email = $"blocked_{Guid.NewGuid():N}@test.com",
            password = IntegrationTestCredentials.DefaultPassword,
            firstName = "Blocked",
            lastName = "Admin",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B15 — ADMIN on infra worker-config → 403.</summary>
    [Fact]
    public async Task InfraWorkerConfig_ReturnsForbidden_ForGlobalAdmin()
    {
        var client = _factory.CreateFaceClient("admin");
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/infra/worker-config");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>ACC-B16 — unauthenticated Stats on admin face → 401.</summary>
    [Fact]
    public async Task Stats_ReturnsUnauthorized_WithoutJwt_OnAdminFace()
    {
        var client = _factory.CreateFaceClient("admin");
        var response = await client.GetAsync("/api/Stats");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>ACC-B17 — CanMutateGlobalPageTypes false for ADMIN on admin face (no pagetypes HTTP route yet).</summary>
    [Fact]
    public void CanMutateGlobalPageTypes_IsFalse_ForGlobalAdmin_OnAdminScope()
    {
        var admin = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, UserRole.GlobalRoleNames.Admin) },
                "Bearer"));

        PlatformAccessRules.CanMutateGlobalPageTypes(
                PlatformAccessRulesTests.AdminFaceScope().Object,
                admin)
            .Should()
            .BeFalse();
    }
}
