using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Search;

namespace BeDemo.Api.Tests;

/// <summary>SAP-B* edge matrix for <see cref="Controllers.AdminMeProfileController"/>.</summary>
public sealed class AdminMeProfileControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _oauth;
    private readonly HttpClient _adminFace;

    public AdminMeProfileControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _oauth = AclTestClients.CreateOAuthClient(factory);
        _adminFace = AclTestClients.CreateAdminFaceClient(factory);
    }

    [Fact]
    public async Task SAP_B1_GetProfile_ReturnsIdentityAndFaces()
    {
        var token = await GetSuperAdminTokenAsync();
        var req = AuthGet("/api/admin/me/profile", token);
        var res = await _adminFace.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("email").GetString().Should().Be(IntegrationTestSeed.SuperAdminEmail);
        json.GetProperty("faces").GetArrayLength().Should().BeGreaterThan(0);
        json.GetProperty("globalRole").GetProperty("name").GetString().Should().Be(UserRole.GlobalRoleNames.SuperAdmin);
    }

    [Fact]
    public async Task SAP_B2_PutProfile_UpdatesEmailAndNames()
    {
        var token = await GetSuperAdminTokenAsync();
        var userId = await GetSuperAdminUserIdAsync();
        var unique = $"super.profile.{Guid.NewGuid():N}@test.com";

        var put = AuthJson(HttpMethod.Put, "/api/admin/me/profile", token, new
        {
            email = unique,
            firstName = "Profile",
            lastName = "Tester",
        });
        (await _adminFace.SendAsync(put)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .AsNoTracking()
            .FirstAsync(u => u.Id == userId);
        user.Email.Should().Be(unique);
        user.UserName.Should().Be(unique);
        user.FirstName.Should().Be("Profile");
        user.LastName.Should().Be("Tester");
    }

    [Fact]
    public async Task SAP_B3_PutProfile_DuplicateEmail_Returns409()
    {
        var token = await GetSuperAdminTokenAsync();
        var put = AuthJson(HttpMethod.Put, "/api/admin/me/profile", token, new { email = IntegrationTestSeed.Email });
        var res = await _adminFace.SendAsync(put);
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SAP_B4_PutProfile_GlobalUserRoleIdInBody_Returns400()
    {
        var token = await GetSuperAdminTokenAsync();
        var put = AuthJson(HttpMethod.Put, "/api/admin/me/profile", token, new { userRoleId = 1 });
        var res = await _adminFace.SendAsync(put);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SAP_B5_PatchFaceRole_UpdatesMembership()
    {
        var token = await GetSuperAdminTokenAsync();
        var userId = await GetSuperAdminUserIdAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminFace = await db.Faces.AsNoTracking().FirstAsync(f => f.Index == "admin");
        var faceAdminRole = await db.UserRoles.AsNoTracking()
            .FirstAsync(r => r.Name == UserRole.FaceRoleNames.FaceAdmin);

        var patch = AuthJson(
            HttpMethod.Patch,
            $"/api/admin/me/faces/{adminFace.Id}/role",
            token,
            new { userRoleId = faceAdminRole.Id });
        (await _adminFace.SendAsync(patch)).StatusCode.Should().Be(HttpStatusCode.OK);

        var row = await db.UserFaceRoles.AsNoTracking()
            .FirstAsync(ufr => ufr.UserId == userId && ufr.FaceId == adminFace.Id);
        row.UserRoleId.Should().Be(faceAdminRole.Id);
    }

    [Fact]
    public async Task SAP_B6_PatchFaceRole_GlobalScopeRoleId_Returns400()
    {
        var token = await GetSuperAdminTokenAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var faceId = await db.Faces.AsNoTracking().Select(f => f.Id).FirstAsync();
        var superRole = await db.UserRoles.AsNoTracking()
            .FirstAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin);

        var patch = AuthJson(
            HttpMethod.Patch,
            $"/api/admin/me/faces/{faceId}/role",
            token,
            new { userRoleId = superRole.Id });
        (await _adminFace.SendAsync(patch)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SAP_B7_PatchFaceRole_NotMember_Returns404()
    {
        var token = await GetSuperAdminTokenAsync();
        var patch = AuthJson(HttpMethod.Patch, "/api/admin/me/faces/999999/role", token, new { userRoleId = 1 });
        (await _adminFace.SendAsync(patch)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SAP_B8_PutPassword_WrongCurrent_Returns400()
    {
        var token = await GetSuperAdminTokenAsync();
        var put = AuthJson(HttpMethod.Put, "/api/admin/me/password", token, new
        {
            currentPassword = "WrongPassword1!",
            newPassword = "NewPassword1!@",
            confirmPassword = "NewPassword1!@",
        });
        var res = await _adminFace.SendAsync(put);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SAP_B9_PutPassword_Success_IncrementsAtvAndRevokesRefreshTokens()
    {
        var token = await GetSuperAdminTokenAsync();
        var userId = await GetSuperAdminUserIdAsync();
        var newPassword = $"NewPw{Guid.NewGuid():N}!1";

        int initialAtv;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
            initialAtv = user.AccessTokenVersion;
            db.OAuthRefreshTokens.Add(new OAuthRefreshToken
            {
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("sap-b9-refresh"))).ToLowerInvariant(),
                UserId = userId,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        try
        {
            var put = AuthJson(HttpMethod.Put, "/api/admin/me/password", token, new
            {
                currentPassword = IntegrationTestSeed.Password,
                newPassword,
                confirmPassword = newPassword,
            });
            (await _adminFace.SendAsync(put)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            using var verifyScope = _factory.Services.CreateScope();
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
            user.AccessTokenVersion.Should().BeGreaterThan(initialAtv);
            var refresh = await db.OAuthRefreshTokens.FirstAsync(r => r.UserId == userId);
            refresh.RevokedAtUtc.Should().NotBeNull();
        }
        finally
        {
            await ResetSuperAdminPasswordAsync(userId, IntegrationTestSeed.Password);
        }
    }

    [Fact]
    public async Task SAP_B10_UnauthorizedAndForbidden()
    {
        (await _adminFace.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/admin/me/profile")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var adminToken = await AclTestClients.GetGlobalAdminTokenAsync(_oauth);
        var req = AuthGet("/api/admin/me/profile", adminToken);
        (await _adminFace.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SAP_B11_PutEmail_StagesSearchOutbox()
    {
        await using var searchFactory = new SearchEnabledCustomWebApplicationFactory();
        await RestoreSuperAdminInFactoryAsync(searchFactory);
        var oauth = AclTestClients.CreateOAuthClient(searchFactory);
        var adminFace = AclTestClients.CreateAdminFaceClient(searchFactory);
        var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(oauth);
        var unique = $"search.outbox.{Guid.NewGuid():N}@test.com";
        var put = AuthJson(HttpMethod.Put, "/api/admin/me/profile", token, new { email = unique });
        (await adminFace.SendAsync(put)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = searchFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasOutbox = await db.SearchOutboxEntries.AsNoTracking()
            .AnyAsync(e => e.DocumentType == SearchDocumentTypes.User && e.Operation == SearchOutboxOperation.Index);
        hasOutbox.Should().BeTrue();

        await RestoreSuperAdminInFactoryAsync(searchFactory);
    }

    [Fact]
    public async Task SAP_B12_PostAvatar_OnAdminFaceJwt_Succeeds()
    {
        var token = await GetSuperAdminTokenAsync();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(MinimalPngBytes), "file", "avatar.png");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/me/avatar") { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _adminFace.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("avatarUrl").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SAP_B13_PutEmail_SetsUnconfirmed_AndEnqueuesMailer()
    {
        _factory.CapturingMailer.Reset();
        var token = await GetSuperAdminTokenAsync();
        var userId = await GetSuperAdminUserIdAsync();
        var unique = $"confirm.{Guid.NewGuid():N}@test.com";

        var put = AuthJson(HttpMethod.Put, "/api/admin/me/profile", token, new { email = unique });
        (await _adminFace.SendAsync(put)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        user.EmailConfirmed.Should().BeFalse();

        _factory.CapturingMailer.LastRequest.Should().NotBeNull();
        _factory.CapturingMailer.LastRequest!.TemplateId.Should().Be(MailTemplateIds.IdentityEmailConfirm);
        _factory.CapturingMailer.LastRequest.To.Should().Contain(unique);
    }

    [Fact]
    public async Task SAP_B14_ConfirmEmail_ValidToken_SetsConfirmed()
    {
        var userId = await GetSuperAdminUserIdAsync();
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            user!.EmailConfirmed = false;
            await userManager.UpdateAsync(user);
            token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        }

        var res = await _adminFace.GetAsync(
            $"/api/auth/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var userManager2 = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var confirmed = await userManager2.FindByIdAsync(userId);
        confirmed!.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmail_Anonymous_InvalidToken_Returns400()
    {
        var userId = await GetSuperAdminUserIdAsync();
        var res = await _adminFace.GetAsync($"/api/auth/confirm-email?userId={Uri.EscapeDataString(userId)}&token=invalid");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static readonly byte[] MinimalPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    private static HttpRequestMessage AuthGet(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private static HttpRequestMessage AuthJson(HttpMethod method, string url, string token, object body)
    {
        var req = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private async Task<string> GetSuperAdminUserIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var superRole = await db.UserRoles.AsNoTracking()
            .FirstAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin);
        return (await db.Users.AsNoTracking().FirstAsync(u => u.UserRoleId == superRole.Id)).Id;
    }

    private async Task ResetSuperAdminPasswordAsync(string userId, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            return;
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await userManager.ResetPasswordAsync(user, token, password);
    }

    private async Task RestoreSuperAdminInFactoryAsync(CustomWebApplicationFactory<Program> factory)
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
    }

    private async Task<string> GetSuperAdminTokenAsync()
    {
        await RestoreSuperAdminIdentityAsync();
        return await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
    }

    private async Task RestoreSuperAdminIdentityAsync()
    {
        await RestoreSuperAdminInFactoryAsync(_factory);
    }

    private sealed class SearchEnabledCustomWebApplicationFactory : CustomWebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Search:Enabled", "true");
            builder.UseSetting("Search:WorkerGrpcUrl", "http://localhost:59996");
        }
    }
}
