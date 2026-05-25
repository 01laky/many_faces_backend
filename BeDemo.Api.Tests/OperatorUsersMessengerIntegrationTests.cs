using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

public class OperatorUsersMessengerIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _adminFace;
	private readonly HttpClient _publicFace;
	private readonly HttpClient _basicFace;

	public OperatorUsersMessengerIntegrationTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_adminFace = AclTestClients.CreateAdminFaceClient(factory);
		_publicFace = AclTestClients.CreatePublicFaceClient(factory);
		_basicFace = factory.CreateFaceClient("basic");
	}

	[Fact]
	public async Task GetDetail_Should401_WithoutJwt()
	{
		var res = await _adminFace.GetAsync("/api/operator-users/users/any/detail");
		res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetDetail_Should403_ForTenantUserOnPublicScope()
	{
		var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var req = new HttpRequestMessage(HttpMethod.Get, "/api/operator-users/users/any/detail");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var res = await _publicFace.SendAsync(req);
		res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GlobalBan_ShouldBlockPasswordGrant_AndRestoreAfterUnban()
	{
		var (token, userId, email) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		token.Should().NotBeNullOrEmpty();

		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var banRes = await PostGlobalBanAsync(superToken, userId);
		banRes.StatusCode.Should().Be(HttpStatusCode.OK);
		await AssertUserLockedOutAsync(userId, true);

		var lockedLogin = await PasswordGrantAsync(email);
		lockedLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

		await DeleteGlobalBanAsync(superToken, userId);
		var restored = await PasswordGrantAsync(email);
		restored.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GlobalBan_ShouldInvalidatePriorAccessToken()
	{
		var (token, userId, email) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);

		(await PostGlobalBanAsync(superToken, userId)).StatusCode.Should().Be(HttpStatusCode.OK);

		var res = await GetConversationsAsync(_publicFace, token);
		res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

		await DeleteGlobalBanAsync(superToken, userId);
		_ = await PasswordGrantAsync(email);
	}

	[Fact]
	public async Task GlobalBan_ShouldBeIdempotent_OnSecondPost()
	{
		var (_, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var first = await PostGlobalBanAsync(superToken, userId);
		first.StatusCode.Should().Be(HttpStatusCode.OK);
		var second = await PostGlobalBanAsync(superToken, userId);
		second.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await second.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("alreadyBanned").GetBoolean().Should().BeTrue();
		await DeleteGlobalBanAsync(superToken, userId);
	}

	[Fact]
	public async Task FaceBan_Should403PeerMessages_OnBannedFace_NotOnOtherFace()
	{
		var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var (_, userB, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		await SeedPeerMessageAsync(userA, userB, "peer hello");

		var publicFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_publicFace, tokenA, "public");
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		await PostFaceBanAsync(superToken, userA, publicFaceId);

		var peerOnPublic = await GetMessagesWithAsync(_publicFace, tokenA, userB);
		peerOnPublic.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		(await peerOnPublic.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
			.Should().Be("face_banned");

		var peerOnBasic = await GetMessagesWithAsync(_basicFace, tokenA, userB);
		peerOnBasic.StatusCode.Should().Be(HttpStatusCode.OK);

		await DeleteFaceBanAsync(superToken, userA, publicFaceId);
	}

	[Fact]
	public async Task FaceBan_ShouldStillAllowSuperAdminThread_InBannedFace()
	{
		var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superId = await GetUserIdByEmailAsync(IntegrationTestSeed.SuperAdminEmail);
		await SeedPeerMessageAsync(superId, userA, "platform hello");

		var publicFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_publicFace, tokenA, "public");
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		await PostFaceBanAsync(superToken, userA, publicFaceId);

		var withSuper = await GetMessagesWithAsync(_publicFace, tokenA, superId);
		withSuper.StatusCode.Should().Be(HttpStatusCode.OK);

		await DeleteFaceBanAsync(superToken, userA, publicFaceId);
	}

	[Fact]
	public async Task FaceBan_ShouldHidePeerFromConversationsList()
	{
		var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var (_, userB, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		await SeedPeerMessageAsync(userA, userB, "list filter peer");

		var publicFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_publicFace, tokenA, "public");
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		await PostFaceBanAsync(superToken, userA, publicFaceId);

		var list = await GetConversationsAsync(_publicFace, tokenA);
		list.StatusCode.Should().Be(HttpStatusCode.OK);
		var items = await list.Content.ReadFromJsonAsync<JsonElement[]>();
		items!.Should().NotContain(e => e.GetProperty("otherUserId").GetString() == userB);

		await DeleteFaceBanAsync(superToken, userA, publicFaceId);
	}

	[Fact]
	public async Task PlatformMessage_ShouldBeReadableInMultipleFaceScopes()
	{
		var (targetToken, targetUserId, _) =
			await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var superId = await GetUserIdByEmailAsync(IntegrationTestSeed.SuperAdminEmail);
		const string content = "multi-face platform ping";
		(await PostPlatformMessageAsync(superToken, targetUserId, content)).StatusCode.Should().Be(HttpStatusCode.OK);

		var onPublic = await GetMessagesWithAsync(_publicFace, targetToken, superId);
		onPublic.StatusCode.Should().Be(HttpStatusCode.OK);
		var publicJson = await onPublic.Content.ReadFromJsonAsync<JsonElement[]>();
		publicJson!.Should().Contain(m => m.GetProperty("content").GetString()!.Contains("multi-face"));

		var onBasic = await GetMessagesWithAsync(_basicFace, targetToken, superId);
		onBasic.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task GetDetail_ShouldReportActiveFaceBanCount()
	{
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var publicFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_publicFace, token, "public");
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		await PostFaceBanAsync(superToken, userId, publicFaceId);

		var detailReq = new HttpRequestMessage(HttpMethod.Get, $"/api/operator-users/users/{userId}/detail");
		detailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
		var detailRes = await _adminFace.SendAsync(detailReq);
		detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await detailRes.Content.ReadFromJsonAsync<JsonElement>();
		json.GetProperty("badges").GetProperty("activeFaceBanCount").GetInt32().Should().BeGreaterThan(0);
		json.GetProperty("faces").EnumerateArray().Should().Contain(f =>
			f.GetProperty("faceId").GetInt32() == publicFaceId && f.GetProperty("isFaceBanned").GetBoolean());

		await DeleteFaceBanAsync(superToken, userId, publicFaceId);
	}

	[Fact]
	public async Task FaceUnban_Should204_WhenNotBanned()
	{
		var (_, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var res = await SendOperatorAsync(
			HttpMethod.Delete,
			$"/api/operator-users/users/{userId}/faces/99999/ban",
			superToken);
		res.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound);
	}

	private async Task SeedPeerMessageAsync(string senderId, string receiverId, string content)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		db.Messages.Add(new Message
		{
			SenderId = senderId,
			ReceiverId = receiverId,
			Content = content,
			IsMessageRequest = false,
		});
		await db.SaveChangesAsync();
	}

	private static Task<HttpResponseMessage> GetMessagesWithAsync(HttpClient faceClient, string token, string otherUserId)
	{
		var req = new HttpRequestMessage(HttpMethod.Get, $"/api/messages/with/{otherUserId}");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return faceClient.SendAsync(req);
	}

	private static Task<HttpResponseMessage> GetConversationsAsync(HttpClient faceClient, string token)
	{
		var req = new HttpRequestMessage(HttpMethod.Get, "/api/messages/conversations");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return faceClient.SendAsync(req);
	}

	private Task<HttpResponseMessage> PostGlobalBanAsync(string superToken, string targetId) =>
		SendOperatorAsync(HttpMethod.Post, $"/api/operator-users/users/{targetId}/global-ban", superToken,
			new { reason = "integration test global ban" });

	private Task<HttpResponseMessage> DeleteGlobalBanAsync(string superToken, string targetId) =>
		SendOperatorAsync(HttpMethod.Delete, $"/api/operator-users/users/{targetId}/global-ban", superToken);

	private Task<HttpResponseMessage> PostFaceBanAsync(string superToken, string targetId, int faceId) =>
		SendOperatorAsync(HttpMethod.Post, $"/api/operator-users/users/{targetId}/faces/{faceId}/ban", superToken,
			new { reason = "integration test face ban" });

	private Task<HttpResponseMessage> DeleteFaceBanAsync(string superToken, string targetId, int faceId) =>
		SendOperatorAsync(HttpMethod.Delete, $"/api/operator-users/users/{targetId}/faces/{faceId}/ban", superToken);

	private Task<HttpResponseMessage> PostPlatformMessageAsync(string superToken, string targetId, string content) =>
		SendOperatorAsync(HttpMethod.Post, $"/api/operator-users/users/{targetId}/platform-messages", superToken,
			new { content });

	private Task<HttpResponseMessage> SendOperatorAsync(
		HttpMethod method,
		string path,
		string superToken,
		object? body = null)
	{
		var req = new HttpRequestMessage(method, path);
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
		if (body != null)
			req.Content = JsonContent.Create(body);
		return _adminFace.SendAsync(req);
	}

	private Task<HttpResponseMessage> PasswordGrantAsync(string email)
	{
		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = email,
			Password = IntegrationTestCredentials.DefaultPassword,
		};
		return _oauth.PostAsJsonAsync("/api/oauth2/token", tokenRequest);
	}

	private async Task AssertUserLockedOutAsync(string userId, bool expected)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var stored = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
		stored.LockoutEnabled.Should().Be(expected);
		if (expected)
			stored.LockoutEnd.Should().NotBeNull();

		var userManager = scope.ServiceProvider
			.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
		var user = await userManager.FindByIdAsync(userId);
		user.Should().NotBeNull();
		(await userManager.IsLockedOutAsync(user!)).Should().Be(expected);
	}

	private async Task<string> GetUserIdByEmailAsync(string email)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == email);
		return user.Id;
	}
}
