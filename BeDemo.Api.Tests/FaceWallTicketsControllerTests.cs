using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Tests;

public class FaceWallTicketsControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private const string WallTicketsTestPassword = "Test1234!@##";

    private readonly CustomWebApplicationFactory<Program> _factory;

    public FaceWallTicketsControllerTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    private static async Task<(string Token, string UserId)> LoginWithPasswordAsync(HttpClient client, string email, string password)
    {
        var tokenRequest = new OAuth2TokenRequest
        {
            GrantType = "password",
            ClientId = "be-demo-client",
            ClientSecret = "be-demo-secret-very-strong-key",
            Username = email,
            Password = password,
        };

        HttpResponseMessage? response = null;
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(150 * (i + 1));
            response = await client.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
            if (response.StatusCode == HttpStatusCode.OK)
                break;
        }

        response!.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>();
        var token = tokenResponse!.AccessToken;
        var payload = token.Split('.')[1];
        var pad = payload.Length % 4 == 0 ? "" : new string('=', 4 - payload.Length % 4);
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload + pad));
        var doc = JsonDocument.Parse(json);
        var userId = doc.RootElement.TryGetProperty("nameid", out var n) ? n.GetString() : doc.RootElement.GetProperty("sub").GetString();
        userId.Should().NotBeNullOrEmpty();
        return (token, userId!);
    }

    private Task<(string Token, string UserId, string Email)> RegisterAndLoginAsync(HttpClient client) =>
        IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
            client,
            _factory,
            $"wt_{Guid.NewGuid():N}@test.com",
            WallTicketsTestPassword,
            "Wall",
            "Tester");

    private static async Task<int> GetAnyFaceIdAsync(HttpClient client, string token) =>
        await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");

    private static async Task<int> GetFaceRoleIdAsync(HttpClient client, string token, string exactName)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var roles = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
        roles.Should().NotBeNull();
        foreach (var r in roles!)
        {
            var name = r.GetProperty("name").GetString() ?? "";
            if (string.Equals(name, exactName, StringComparison.Ordinal))
                return r.GetProperty("id").GetInt32();
        }

        throw new InvalidOperationException($"Role {exactName} not found");
    }

    private static async Task<string> PromoteUserToGlobalAdminAsync(
        CustomWebApplicationFactory<Program> factory,
        HttpClient client,
        string userId,
        string userEmail)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminRole = await db.UserRoles.AsNoTracking()
            .FirstAsync(r => r.Name == UserRole.GlobalRoleNames.Admin);
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.UserRoleId = adminRole.Id;
        await db.SaveChangesAsync();

        var (token, _) = await LoginWithPasswordAsync(client, userEmail, WallTicketsTestPassword);
        return token;
    }

    [Fact]
    public async Task Create_ShouldFail_WhenHostRole()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var hostRoleId = await GetFaceRoleIdAsync(client, token, "FACE_HOST");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = hostRoleId })).EnsureSuccessStatusCode();

        var res = await client.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_List_Get_Comment_Like_Approve_FreezeInteractions()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();
        using var clientAdmin = _factory.CreateClient();
        var (tokenA, _, _) = await RegisterAndLoginAsync(clientA);
        var (tokenB, _, _) = await RegisterAndLoginAsync(clientB);
        var (tokenAdmin, userIdAdmin, adminEmail) = await RegisterAndLoginAsync(clientAdmin);
        var faceId = await GetAnyFaceIdAsync(clientA, tokenA);
        var roleId = await GetFaceRoleIdAsync(clientA, tokenA, "FACE_USER");

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await clientA.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        (await clientB.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var create = await clientA.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets",
            new { title = " Idea ", description = " Body text " });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var like = await clientB.PostAsync($"/api/faces/{faceId}/wall-tickets/{ticketId}/like", null);
        like.StatusCode.Should().Be(HttpStatusCode.OK);
        var comment = await clientB.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets/{ticketId}/comments",
            new { content = "hello" });
        comment.StatusCode.Should().Be(HttpStatusCode.Created);

        tokenAdmin = await PromoteUserToGlobalAdminAsync(_factory, clientAdmin, userIdAdmin, adminEmail);
        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin);
        var approve = await clientAdmin.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var likeAfter = await clientB.PostAsync($"/api/faces/{faceId}/wall-tickets/{ticketId}/like", null);
        likeAfter.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var commentAfter = await clientB.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets/{ticketId}/comments",
            new { content = "nope" });
        commentAfter.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deny_ThenLifecycle_DeletesHard()
    {
        using var clientAuthor = _factory.CreateClient();
        using var clientAdmin = _factory.CreateClient();
        var (tokenA, _, _) = await RegisterAndLoginAsync(clientAuthor);
        var (tokenAdmin, userIdAdmin, adminEmail) = await RegisterAndLoginAsync(clientAdmin);
        var faceId = await GetAnyFaceIdAsync(clientAuthor, tokenA);
        var roleId = await GetFaceRoleIdAsync(clientAuthor, tokenA, "FACE_USER");
        clientAuthor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await clientAuthor.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var create = await clientAuthor.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets",
            new { title = "X", description = "Y" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        tokenAdmin = await PromoteUserToGlobalAdminAsync(_factory, clientAdmin, userIdAdmin, adminEmail);
        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin);
        var deny = await clientAdmin.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/deny", null);
        deny.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var life = scope.ServiceProvider.GetRequiredService<IFaceWallTicketLifecycleService>();
            await life.DeleteTicketHardAsync(ticketId);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.FaceWallTickets.AnyAsync(t => t.Id == ticketId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task AuthorCannotDeleteComment_AdminCan()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();
        var (tokenA, userA, emailA) = await RegisterAndLoginAsync(clientA);
        var (tokenB, _, _) = await RegisterAndLoginAsync(clientB);
        var faceId = await GetAnyFaceIdAsync(clientA, tokenA);
        var roleId = await GetFaceRoleIdAsync(clientA, tokenA, "FACE_USER");

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await clientA.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        (await clientB.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var create = await clientA.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        var comment = await clientB.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets/{ticketId}/comments",
            new { content = "c1" });
        var commentId = (await comment.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        // No user endpoint for author to delete comment — only admin API
        tokenA = await PromoteUserToGlobalAdminAsync(_factory, clientA, userA, emailA);
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var del = await clientA.DeleteAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/comments/{commentId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Max20Tickets_Enforced()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        for (var i = 0; i < 20; i++)
        {
            var r = await client.PostAsJsonAsync(
                $"/api/faces/{faceId}/wall-tickets",
                new { title = $"T{i}", description = "D" });
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var fail = await client.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets",
            new { title = "Overflow", description = "D" });
        fail.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonAdmin_CannotAccessAdminList()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var res = await client.GetAsync($"/api/admin/faces/{faceId}/wall-tickets");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Author_CannotEditOrDelete_WhenApproved_GlobalAdminCanDeleteViaUserApi()
    {
        using var clientAuthor = _factory.CreateClient();
        using var clientAdmin = _factory.CreateClient();
        var (tokenA, _, _) = await RegisterAndLoginAsync(clientAuthor);
        var (tokenAdmin, userIdAdmin, adminEmail) = await RegisterAndLoginAsync(clientAdmin);
        var faceId = await GetAnyFaceIdAsync(clientAuthor, tokenA);
        var roleId = await GetFaceRoleIdAsync(clientAuthor, tokenA, "FACE_USER");

        clientAuthor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await clientAuthor.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var create = await clientAuthor.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        var putOk = await clientAuthor.PutAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets/{ticketId}",
            new { title = "T2", description = "D2" });
        putOk.StatusCode.Should().Be(HttpStatusCode.OK);

        tokenAdmin = await PromoteUserToGlobalAdminAsync(_factory, clientAdmin, userIdAdmin, adminEmail);
        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin);
        (await clientAdmin.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/approve", null)).EnsureSuccessStatusCode();

        clientAuthor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var putBad = await clientAuthor.PutAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets/{ticketId}",
            new { title = "T3", description = "D3" });
        putBad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var delAuthor = await clientAuthor.DeleteAsync($"/api/faces/{faceId}/wall-tickets/{ticketId}");
        delAuthor.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var delAdmin = await clientAdmin.DeleteAsync($"/api/faces/{faceId}/wall-tickets/{ticketId}");
        delAdmin.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Comment_TooLong_Returns400()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var create = await client.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        var longText = new string('x', 256);
        var res = await client.PostAsJsonAsync(
            $"/api/faces/{faceId}/wall-tickets/{ticketId}/comments",
            new { content = longText });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Returns404_WhenFaceNotFound()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.GetAsync("/api/faces/999999999/wall-tickets");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deny_ThenLike_ReturnsBadRequest()
    {
        using var clientAuthor = _factory.CreateClient();
        using var clientAdmin = _factory.CreateClient();
        var (tokenA, _, _) = await RegisterAndLoginAsync(clientAuthor);
        var (tokenAdmin, userIdAdmin, adminEmail) = await RegisterAndLoginAsync(clientAdmin);
        var faceId = await GetAnyFaceIdAsync(clientAuthor, tokenA);
        var roleId = await GetFaceRoleIdAsync(clientAuthor, tokenA, "FACE_USER");
        clientAuthor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await clientAuthor.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();

        var ticketId = (await (await clientAuthor.PostAsJsonAsync(
                $"/api/faces/{faceId}/wall-tickets",
                new { title = "T", description = "D" }))
            .Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        tokenAdmin = await PromoteUserToGlobalAdminAsync(_factory, clientAdmin, userIdAdmin, adminEmail);
        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin);
        (await clientAdmin.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/deny", null)).EnsureSuccessStatusCode();

        var like = await clientAuthor.PostAsync($"/api/faces/{faceId}/wall-tickets/{ticketId}/like", null);
        like.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApproveTwice_Returns400()
    {
        using var clientA = _factory.CreateClient();
        using var clientAdmin = _factory.CreateClient();
        var (tokenA, _, _) = await RegisterAndLoginAsync(clientA);
        var (tokenAdmin, userIdAdmin, adminEmail) = await RegisterAndLoginAsync(clientAdmin);
        var faceId = await GetAnyFaceIdAsync(clientA, tokenA);
        var roleId = await GetFaceRoleIdAsync(clientA, tokenA, "FACE_USER");
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await clientA.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();
        var ticketId = (await (await clientA.PostAsJsonAsync(
                $"/api/faces/{faceId}/wall-tickets",
                new { title = "T", description = "D" }))
            .Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();

        tokenAdmin = await PromoteUserToGlobalAdminAsync(_factory, clientAdmin, userIdAdmin, adminEmail);
        clientAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin);
        (await clientAdmin.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/approve", null)).EnsureSuccessStatusCode();
        var again = await clientAdmin.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/approve", null);
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(HttpClient Client, string Token, int FaceId)> NewAdminWithPublicFaceAsync()
    {
        var client = _factory.CreateClient();
        var (token, userId, email) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        var roleId = await GetFaceRoleIdAsync(client, token, "FACE_USER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId })).EnsureSuccessStatusCode();
        token = await PromoteUserToGlobalAdminAsync(_factory, client, userId, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token, faceId);
    }

    [Fact]
    public async Task AdminCreate_ShouldReturn201_WithActiveStatus()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var res = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "Operator idea", description = "Backlog body" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body!.GetProperty("status").GetString().Should().Be("active");
        body.GetProperty("title").GetString().Should().Be("Operator idea");
    }

    [Fact]
    public async Task AdminCreate_ManyTickets_ShouldNotHitUserCap()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        for (var i = 0; i < 25; i++)
        {
            var res = await client.PostAsJsonAsync(
                $"/api/admin/faces/{faceId}/wall-tickets",
                new { title = $"Op{i}", description = "D" });
            res.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task AdminCreate_OnMissingFace_ShouldReturn404()
    {
        var (client, _, _) = await NewAdminWithPublicFaceAsync();
        var res = await client.PostAsJsonAsync(
            "/api/admin/faces/999999999/wall-tickets",
            new { title = "T", description = "D" });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminCreate_NonAdmin_ShouldReturn403()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminCreate_EmptyFields_ShouldReturn400()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var res = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "", description = "" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminCreate_TitleTooLong_ShouldReturn400()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var res = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = new string('x', 201), description = "D" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminComment_OnActive_ShouldReturn201_AndAppearInDetail()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var create = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        var comment = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/comments",
            new { content = "Staff note" });
        comment.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}");
        detail!.GetProperty("comments").EnumerateArray()
            .Any(c => c.GetProperty("content").GetString() == "Staff note")
            .Should().BeTrue();
    }

    [Fact]
    public async Task AdminComment_OnApproved_ShouldReturn400()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var create = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        (await client.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/approve", null)).EnsureSuccessStatusCode();
        var comment = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/comments",
            new { content = "nope" });
        comment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminComment_WrongFaceId_ShouldReturn404()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var create = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        var comment = await client.PostAsJsonAsync(
            $"/api/admin/faces/999999999/wall-tickets/{ticketId}/comments",
            new { content = "x" });
        comment.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminComment_NonAdmin_ShouldReturn403()
    {
        using var client = _factory.CreateClient();
        var (token, _, _) = await RegisterAndLoginAsync(client);
        var faceId = await GetAnyFaceIdAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets/1/comments",
            new { content = "x" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminComment_TooLong_ShouldReturn400()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var create = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        var res = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/comments",
            new { content = new string('c', 256) });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminGetTicket_WrongFaceId_ShouldReturn404()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var create = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        var res = await client.GetAsync($"/api/admin/faces/999999999/wall-tickets/{ticketId}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminList_InvalidStatusFilter_ShouldReturn400()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var res = await client.GetAsync($"/api/admin/faces/{faceId}/wall-tickets?status=notastatus");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminList_StatusApproved_ShouldReturnOnlyApproved()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var create = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        (await client.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/approve", null)).EnsureSuccessStatusCode();
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/admin/faces/{faceId}/wall-tickets?status=approved");
        foreach (var item in list!.GetProperty("items").EnumerateArray())
            item.GetProperty("status").GetString().Should().Be("approved");
    }

    [Fact]
    public async Task DenyThenApprove_ShouldReturn400()
    {
        var (client, _, faceId) = await NewAdminWithPublicFaceAsync();
        var create = await client.PostAsJsonAsync(
            $"/api/admin/faces/{faceId}/wall-tickets",
            new { title = "T", description = "D" });
        var ticketId = (await create.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetInt32();
        (await client.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/deny", null)).EnsureSuccessStatusCode();
        var approve = await client.PostAsync($"/api/admin/faces/{faceId}/wall-tickets/{ticketId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
