using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Hubs;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// SignalR edge cases for super-admin platform DMs: user replies must persist even when the
/// super-admin is not a member of the user's tenant face (MessengerHub bypasses tenant social scope).
/// </summary>
public sealed class MessengerHubPlatformDirectMessageTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _adminFace;
	private readonly HttpClient _publicFace;

	public MessengerHubPlatformDirectMessageTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_adminFace = AclTestClients.CreateAdminFaceClient(factory);
		_publicFace = AclTestClients.CreatePublicFaceClient(factory);
	}

	[Fact]
	public async Task UserReply_ViaSendChatMessage_ShouldPersist_WhenSuperAdminNotOnPublicFace()
	{
		var (userToken, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var superId = await GetUserIdByEmailAsync(IntegrationTestSeed.SuperAdminEmail);
		var publicFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(_publicFace, userToken, "public");
		await EnsureSuperAdminNotOnPublicFaceAsync(publicFaceId, superId, userId);

		(await PostPlatformMessageAsync(superToken, userId, "seed platform thread for hub reply")).StatusCode
			.Should()
			.Be(HttpStatusCode.OK);

		const string reply = "user hub reply without shared face membership";
		await using var userHub = await ConnectMessengerHubAsync(_publicFace, userToken);
		await userHub.InvokeAsync("SendChatMessage", superId, reply);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var row = await db.Messages.AsNoTracking()
				.FirstOrDefaultAsync(m => m.SenderId == userId && m.ReceiverId == superId && m.Content == reply);
			row.Should().NotBeNull();
			row!.IsPlatformDirectMessage.Should().BeTrue();
			row.IsMessageRequest.Should().BeFalse();
		}

		var historyReq = new HttpRequestMessage(HttpMethod.Get, $"/api/operator-user-chat/with/{userId}");
		historyReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
		var historyRes = await _adminFace.SendAsync(historyReq);
		historyRes.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await historyRes.Content.ReadFromJsonAsync<JsonElement>();
		json.GetProperty("items").EnumerateArray().Should()
			.Contain(m => m.GetProperty("content").GetString() == reply);
	}

	[Fact]
	public async Task UserReply_ViaSendChatMessage_ShouldEmitNoPlatformThreadError_WhenThreadMissing()
	{
		var (userToken, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superId = await GetUserIdByEmailAsync(IntegrationTestSeed.SuperAdminEmail);

		await using var userHub = await ConnectMessengerHubAsync(_publicFace, userToken);
		var errorTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		userHub.On<string>("ReceivePlatformChatError", code => errorTcs.TrySetResult(code));

		await userHub.InvokeAsync("SendChatMessage", superId, "reply without seeded thread");

		var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
		completed.Should().Be(errorTcs.Task, "hub should surface platform thread errors to the caller");
		(await errorTcs.Task).Should().Be(OperatorUserChatHubErrorCodes.NoPlatformThread);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var stray = await db.Messages.AsNoTracking()
			.AnyAsync(m => m.SenderId == userId && m.ReceiverId == superId && m.Content == "reply without seeded thread");
		stray.Should().BeFalse();
	}

	[Fact]
	public async Task UserReply_ViaSendChatMessage_ShouldAppearInPortalMessagesApi()
	{
		var (userToken, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var superId = await GetUserIdByEmailAsync(IntegrationTestSeed.SuperAdminEmail);

		(await PostPlatformMessageAsync(superToken, userId, "seed for portal history")).StatusCode
			.Should()
			.Be(HttpStatusCode.OK);

		const string reply = "portal-visible user reply";
		await using var userHub = await ConnectMessengerHubAsync(_publicFace, userToken);
		await userHub.InvokeAsync("SendChatMessage", superId, reply);

		var req = new HttpRequestMessage(HttpMethod.Get, $"/api/messages/with/{superId}");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
		var res = await _publicFace.SendAsync(req);
		res.StatusCode.Should().Be(HttpStatusCode.OK);
		var items = await res.Content.ReadFromJsonAsync<JsonElement[]>();
		items!.Should().Contain(m => m.GetProperty("content").GetString() == reply);
	}

	private async Task EnsureSuperAdminNotOnPublicFaceAsync(int publicFaceId, string superId, string userId)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var superProfileId = await db.UserProfiles.AsNoTracking()
			.Where(p => p.UserId == superId)
			.Select(p => p.Id)
			.FirstAsync();
		var links = await db.UserFaceProfiles
			.Where(ufp => ufp.UserProfileId == superProfileId && ufp.FaceId == publicFaceId)
			.ToListAsync();
		db.UserFaceProfiles.RemoveRange(links);
		await db.SaveChangesAsync();

		var inSameFace = await TenantSocialScopeRules.BothUsersParticipateInFaceAsync(
			db,
			publicFaceId,
			userId,
			superId);
		inSameFace.Should().BeFalse("regression guard: platform DM must not depend on shared face membership");
	}

	private async Task<HubConnection> ConnectMessengerHubAsync(HttpClient faceClient, string accessToken)
	{
		var hubUrl = new Uri(
			faceClient.BaseAddress!,
			$"/public/hubs/messenger?access_token={Uri.EscapeDataString(accessToken)}");
		var connection = new HubConnectionBuilder()
			.WithUrl(hubUrl, options =>
			{
				options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
				options.Transports = HttpTransportType.LongPolling;
			})
			.Build();
		await connection.StartAsync();
		connection.State.Should().Be(HubConnectionState.Connected);
		return connection;
	}

	private Task<HttpResponseMessage> PostPlatformMessageAsync(string superToken, string targetId, string content)
	{
		var req = new HttpRequestMessage(HttpMethod.Post, $"/api/operator-users/users/{targetId}/platform-messages")
		{
			Content = JsonContent.Create(new { content }),
		};
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
		return _adminFace.SendAsync(req);
	}

	private async Task<string> GetUserIdByEmailAsync(string email)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		return (await db.Users.AsNoTracking().FirstAsync(u => u.Email == email)).Id;
	}
}
