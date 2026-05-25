using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs.Admin;
using BeDemo.Api.Models.Requests.Admin;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorMail;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>Integration coverage for <see cref="Controllers.AdminMailSettingsController"/> (AMC-B*).</summary>
public sealed class AdminMailSettingsControllerTests
    : IClassFixture<CustomWebApplicationFactory<Program>>,
        IClassFixture<MailDisabledWebApplicationFactory>,
        IDisposable
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly MailDisabledWebApplicationFactory _mailDisabledFactory;
    private readonly HttpClient _oauth;
    private readonly HttpClient _adminFace;

    public AdminMailSettingsControllerTests(
        CustomWebApplicationFactory<Program> factory,
        MailDisabledWebApplicationFactory mailDisabledFactory)
    {
        _factory = factory;
        _mailDisabledFactory = mailDisabledFactory;
        _oauth = AclTestClients.CreateOAuthClient(factory);
        _adminFace = AclTestClients.CreateAdminFaceClient(factory);
    }

    public void Dispose() { }

    [Fact]
    public async Task AMC_B1_GetSettings_ReturnsBootstrap_WhenNoPriorPut()
    {
        var client = await CreateSuperAdminClientAsync();
        var res = await client.GetAsync("/api/admin/mail/settings");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<AdminMailSettingsDto>();
        dto.Should().NotBeNull();
        dto!.Smtp.Host.Should().NotBeNullOrWhiteSpace();
        dto.From.Email.Should().NotBeNullOrWhiteSpace();
        dto.WorkerGrpcUrl.Should().Contain("59998");
    }

    [Fact]
    public async Task AMC_B2_PutSettings_PersistsEnabledSmtpAndLinks()
    {
        var client = await CreateSuperAdminClientAsync();
        var body = ValidPutBody(enabled: true);
        var res = await client.PutAsJsonAsync("/api/admin/mail/settings", body);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<AdminMailSettingsDto>();
        dto!.Enabled.Should().BeTrue();
        dto.Smtp.Host.Should().Be("smtp.test");
        dto.From.Email.Should().Be("ops@test.invalid");
        dto.RegistrationLinks.PortalPublicBaseUrl.Should().Be("https://portal.test");
    }

    [Fact]
    public async Task AMC_B3_PutSettings_InvalidWorkerUrl_Returns400()
    {
        var client = await CreateSuperAdminClientAsync();
        var body = ValidPutBody();
        body.WorkerGrpcUrl = "not-a-url";
        var res = await client.PutAsJsonAsync("/api/admin/mail/settings", body);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AMC_B4_PutSettings_PathWithoutLocale_Returns400()
    {
        var client = await CreateSuperAdminClientAsync();
        var body = ValidPutBody();
        body.RegistrationLinks!.CompleteRegistrationPathTemplate = "/register/complete";
        var res = await client.PutAsJsonAsync("/api/admin/mail/settings", body);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AMC_B5_GetSettings_NeverReturnsSecrets()
    {
        var client = await CreateSuperAdminClientAsync();
        var put = ValidPutBody();
        put.WorkerAuthToken = "super-secret-token";
        put.Smtp!.Password = "smtp-secret";
        (await client.PutAsJsonAsync("/api/admin/mail/settings", put)).EnsureSuccessStatusCode();

        var json = await (await client.GetAsync("/api/admin/mail/settings")).Content.ReadAsStringAsync();
        json.Should().NotContain("super-secret-token");
        json.Should().NotContain("smtp-secret");
        json.Should().NotContain("workerAuthToken");
        json.Should().NotContain("\"password\"");
    }

    [Fact]
    public async Task AMC_B6_PutRotateWorkerToken_HasFlagWithoutPlaintext()
    {
        var client = await CreateSuperAdminClientAsync();
        var put = ValidPutBody();
        put.WorkerAuthToken = "rotate-me-token";
        (await client.PutAsJsonAsync("/api/admin/mail/settings", put)).EnsureSuccessStatusCode();

        var dto = await (await client.GetAsync("/api/admin/mail/settings")).Content.ReadFromJsonAsync<AdminMailSettingsDto>();
        dto!.HasWorkerAuthToken.Should().BeTrue();
        var json = await (await client.GetAsync("/api/admin/mail/settings")).Content.ReadAsStringAsync();
        json.Should().NotContain("rotate-me-token");
    }

    [Fact]
    public async Task AMC_B7_GetSettings_Returns401WithoutJwt()
    {
        var res = await _adminFace.GetAsync("/api/admin/mail/settings");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AMC_B7b_GetSettings_Returns403ForGlobalAdmin()
    {
        var client = AclTestClients.CreateAdminFaceClient(_factory);
        var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(_oauth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.GetAsync("/api/admin/mail/settings");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AMC_B8_PutDisabled_SkipsMailerSend()
    {
        var client = await CreateSuperAdminClientAsync();
        var put = ValidPutBody(enabled: false);
        (await client.PutAsJsonAsync("/api/admin/mail/settings", put)).EnsureSuccessStatusCode();

        _factory.CapturingMailer.Reset();
        (await client.PostAsync("/api/admin/mailer/test-self", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.CapturingMailer.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task AMC_B10_WorkerConfig_ReflectsEffectiveStatus()
    {
        var client = await CreateSuperAdminClientAsync();
        var put = ValidPutBody(enabled: true);
        (await client.PutAsJsonAsync("/api/admin/mail/settings", put)).EnsureSuccessStatusCode();

        var infra = await (await client.GetAsync("/api/admin/infra/worker-config")).Content.ReadFromJsonAsync<JsonElement>();
        infra.GetProperty("mail").GetProperty("configured").GetBoolean().Should().BeTrue();
        infra.GetProperty("mail").GetProperty("effectiveStatus").GetString().Should().Be("configured");
    }

    [Fact]
    public async Task AMC_B11_PutEnabledWithoutSmtpHost_Returns400()
    {
        var client = await CreateSuperAdminClientAsync();
        var put = ValidPutBody();
        put.Smtp!.Host = "";
        var res = await client.PutAsJsonAsync("/api/admin/mail/settings", put);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AMC_B12_MailerClient_IncludesSmtpBlockOnSend()
    {
        var client = await CreateSuperAdminClientAsync();
        (await client.PutAsJsonAsync("/api/admin/mail/settings", ValidPutBody())).EnsureSuccessStatusCode();

        _factory.CapturingMailer.Reset();
        (await client.PostAsync("/api/admin/mailer/test-self", null)).EnsureSuccessStatusCode();
        _factory.CapturingMailer.LastRequest.Should().NotBeNull();
        _factory.CapturingMailer.LastRequest!.Smtp.Should().NotBeNull();
        _factory.CapturingMailer.LastRequest.Smtp!.Host.Should().Be("smtp.test");
    }

    [Fact]
    public async Task AMC_B13_PutRotateSmtpPassword_HasPasswordWithoutPlaintext()
    {
        var client = await CreateSuperAdminClientAsync();
        var put = ValidPutBody();
        put.Smtp!.Password = "smtp-pass-123";
        (await client.PutAsJsonAsync("/api/admin/mail/settings", put)).EnsureSuccessStatusCode();

        var dto = await (await client.GetAsync("/api/admin/mail/settings")).Content.ReadFromJsonAsync<AdminMailSettingsDto>();
        dto!.Smtp.HasPassword.Should().BeTrue();
        var json = await (await client.GetAsync("/api/admin/mail/settings")).Content.ReadAsStringAsync();
        json.Should().NotContain("smtp-pass-123");
    }

    [Fact]
    public async Task AMC_B9_AdminProfileEmailConfirm_RespectsDisabledMail()
    {
        var client = await CreateSuperAdminClientAsync();
        var put = ValidPutBody(enabled: false);
        (await client.PutAsJsonAsync("/api/admin/mail/settings", put)).EnsureSuccessStatusCode();

        _factory.CapturingMailer.Reset();
        (await client.PostAsync("/api/admin/mailer/test-self", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.CapturingMailer.LastRequest.Should().BeNull();
    }

    private static UpdateAdminMailSettingsRequest ValidPutBody(bool enabled = true) => new()
    {
        Enabled = enabled,
        DefaultLocale = "en",
        WorkerGrpcUrl = "http://localhost:59998",
        Smtp = new UpdateAdminMailSmtpRequest
        {
            Host = "smtp.test",
            Port = 1025,
            StartTls = false,
            User = "",
        },
        From = new UpdateAdminMailFromRequest
        {
            Email = "ops@test.invalid",
            DisplayName = "Ops",
        },
        RegistrationLinks = new UpdateAdminMailRegistrationLinksRequest
        {
            PortalPublicBaseUrl = "https://portal.test",
            CompleteRegistrationPathTemplate = "/{locale}/register/complete",
            MobileDeepLinkBase = "manyfaces://register/complete",
            PreferMobileDeepLinkWhenPlatformMobile = false,
        },
    };

    private async Task<HttpClient> CreateSuperAdminClientAsync(CustomWebApplicationFactory<Program>? factory = null)
    {
        factory ??= _factory;
        await RestoreSuperAdminInFactoryAsync(factory);
        var client = AclTestClients.CreateAdminFaceClient(factory);
        var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task RestoreSuperAdminInFactoryAsync(CustomWebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var superRole = await db.UserRoles.AsNoTracking()
            .FirstAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin);
        var user = await db.Users.FirstAsync(u => u.UserRoleId == superRole.Id);
        user.Email = IntegrationTestSeed.SuperAdminEmail;
        user.UserName = IntegrationTestSeed.SuperAdminEmail;
        user.NormalizedEmail = userManager.NormalizeEmail(user.Email);
        user.NormalizedUserName = userManager.NormalizeName(user.UserName);
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        await userManager.ResetPasswordAsync(user, resetToken, IntegrationTestSeed.Password);
        factory.Services.GetRequiredService<IOperatorMailSettingsProvider>().InvalidateCache();
    }
}
