using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Messenger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP3 edge cases (BE-RP3-U1…U6).</summary>
public sealed class BeRp3ConversationListEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _publicFace;

	public BeRp3ConversationListEdgeTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_publicFace = AclTestClients.CreatePublicFaceClient(factory);
	}

	/// <summary>BE-RP3-U1 — user with no conversations returns empty items.</summary>
	[Fact]
	public async Task BE_RP3_U1_NoConversations_ReturnsEmptyItemsEnvelope()
	{
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		var response = await GetConversationsAsync(token);
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
		var items = IntegrationTestPaginatedList.ReadItems(envelope);
		items.Should().BeEmpty();
		envelope.GetProperty("totalCount").GetInt32().Should().Be(0);
	}

	/// <summary>BE-RP3-U2 — blocked peer excluded from list.</summary>
	[Fact]
	public async Task BE_RP3_U2_BlockedPeer_ExcludedFromConversations()
	{
		var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var (_, userB, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		await SeedMessageAsync(userA, userB, "before block", accepted: true);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.UserBlocks.Add(new UserBlock { BlockerId = userA, BlockedId = userB });
			await db.SaveChangesAsync();
		}

		var response = await GetConversationsAsync(tokenA);
		response.EnsureSuccessStatusCode();
		var items = IntegrationTestPaginatedList.ReadItems(await response.Content.ReadFromJsonAsync<JsonElement>());
		items.Should().NotContain(e => e.GetProperty("otherUserId").GetString() == userB);
	}

	/// <summary>BE-RP3-U3 — pending message requests excluded from conversations list.</summary>
	[Fact]
	public async Task BE_RP3_U3_PendingMessageRequest_ExcludedFromConversations()
	{
		var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var (_, userB, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		await SeedMessageAsync(userB, userA, "pending request", accepted: false, isRequest: true);
		await SeedMessageAsync(userA, userB, "accepted thread", accepted: true);

		var response = await GetConversationsAsync(tokenA);
		response.EnsureSuccessStatusCode();
		var items = IntegrationTestPaginatedList.ReadItems(await response.Content.ReadFromJsonAsync<JsonElement>());
		items.Should().ContainSingle(e => e.GetProperty("otherUserId").GetString() == userB);
		items[0].GetProperty("lastMessage").GetString().Should().Be("accepted thread");
	}

	/// <summary>BE-RP3-U4 — page 2 returns next peers sorted by lastMessageAt desc.</summary>
	[Fact]
	public async Task BE_RP3_U4_Page2_ReturnsNextPeers_StableSort()
	{
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var peerIds = new List<string>();
		for (var i = 0; i < 3; i++)
		{
			var (_, peerId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
			peerIds.Add(peerId);
			var sentAt = DateTime.UtcNow.AddMinutes(-i);
			await SeedMessageAsync(userId, peerId, $"msg-{i}", accepted: true, sentAt: sentAt);
		}

		var page1 = await GetConversationsAsync(token, page: 1, pageSize: 2);
		page1.EnsureSuccessStatusCode();
		var env1 = await page1.Content.ReadFromJsonAsync<JsonElement>();
		env1.GetProperty("totalCount").GetInt32().Should().Be(3);
		var page1Items = IntegrationTestPaginatedList.ReadItems(env1);
		page1Items.Should().HaveCount(2);

		var page2 = await GetConversationsAsync(token, page: 2, pageSize: 2);
		var page2Items = IntegrationTestPaginatedList.ReadItems(await page2.Content.ReadFromJsonAsync<JsonElement>());
		page2Items.Should().HaveCount(1);

		var allIds = page1Items.Concat(page2Items)
			.Select(e => e.GetProperty("otherUserId").GetString())
			.ToList();
		allIds.Should().BeEquivalentTo(peerIds);

		var times = page1Items.Select(e => e.GetProperty("lastMessageAt").GetDateTime()).ToList();
		times.Should().BeInDescendingOrder();
	}

	/// <summary>BE-RP3-U5 — unread count per conversation is correct.</summary>
	[Fact]
	public async Task BE_RP3_U5_UnreadCount_IsCorrectPerConversation()
	{
		var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var (_, userB, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);

		await SeedMessageAsync(userB, userA, "one", accepted: true, read: false);
		await SeedMessageAsync(userB, userA, "two", accepted: true, read: false);
		await SeedMessageAsync(userA, userB, "mine", accepted: true, read: false);

		var response = await GetConversationsAsync(tokenA);
		var items = IntegrationTestPaginatedList.ReadItems(await response.Content.ReadFromJsonAsync<JsonElement>());
		var row = items.Single(e => e.GetProperty("otherUserId").GetString() == userB);
		row.GetProperty("unreadCount").GetInt32().Should().Be(2);
	}

	/// <summary>BE-RP3-U6 — 500 messages across peers uses bounded SQL (no full-table materialization).</summary>
	[Fact]
	public async Task BE_RP3_U6_HighVolume_BoundedQueryCount()
	{
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);
		var (_, peerId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(_oauth, _factory);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var baseTime = DateTime.UtcNow.AddHours(-10);
			for (var i = 0; i < 500; i++)
			{
				var fromA = i % 2 == 0;
				db.Messages.Add(new Message
				{
					SenderId = fromA ? userId : peerId,
					ReceiverId = fromA ? peerId : userId,
					Content = $"bulk-{i}",
					SentAt = baseTime.AddSeconds(i),
					IsMessageRequest = false,
					ReadAt = fromA ? DateTime.UtcNow : null,
				});
			}

			await db.SaveChangesAsync();
		}

		var interceptor = new DbCommandCountInterceptor();
		await using var queryScope = _factory.Services.CreateAsyncScope();
		var sp = queryScope.ServiceProvider;
		var options = sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
		var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>(options)
			.AddInterceptors(interceptor);
		await using var countingDb = new ApplicationDbContext(optionsBuilder.Options);

		var faceScope = new TestFaceScopeContext();
		var service = new ConversationListService(
			countingDb,
			sp.GetRequiredService<IFaceModerationService>(),
			faceScope,
			sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BeDemo.Api.Configuration.PerformanceOptions>>());

		interceptor.Reset();
		var result = await service.GetConversationsAsync(userId, 1, 50, CancellationToken.None);
		interceptor.CommandCount.Should().BeLessThanOrEqualTo(8, "paginated path must not load entire message history into memory");
		result.Items.Should().HaveCount(1);
		result.TotalCount.Should().Be(1);

		var httpRes = await GetConversationsAsync(token, page: 1, pageSize: 50);
		httpRes.EnsureSuccessStatusCode();
	}

	private Task<HttpResponseMessage> GetConversationsAsync(string token, int page = 1, int pageSize = 50)
	{
		var req = new HttpRequestMessage(HttpMethod.Get, $"/api/messages/conversations?page={page}&pageSize={pageSize}");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return _publicFace.SendAsync(req);
	}

	private async Task SeedMessageAsync(
		string senderId,
		string receiverId,
		string content,
		bool accepted,
		bool isRequest = false,
		bool read = true,
		DateTime? sentAt = null)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		db.Messages.Add(new Message
		{
			SenderId = senderId,
			ReceiverId = receiverId,
			Content = content,
			SentAt = sentAt ?? DateTime.UtcNow,
			IsMessageRequest = isRequest,
			MessageRequestStatus = isRequest
				? (accepted ? MessageRequestStatus.Accepted : MessageRequestStatus.Pending)
				: null,
			ReadAt = read ? DateTime.UtcNow : null,
		});
		await db.SaveChangesAsync();
	}

	private sealed class TestFaceScopeContext : IFaceScopeContext
	{
		public bool IsAvailable => true;
		public int FaceId => 1;
		public string FaceIndex => "public";
		public bool IsPublicFace => true;
		public bool IsAdminFaceScope => false;
		public int ResolveDataFaceId(int? queryFaceId) => FaceId;
	}
}
