using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP14 edge cases (BE-RP14-U1…U2).</summary>
public sealed class BeRp14HubDisplayCacheEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp14HubDisplayCacheEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP14-U1 — repeat hub display lookup hits IMemoryCache (one DB read).</summary>
	[Fact]
	public async Task BE_RP14_U1_RepeatLookup_UsesCacheBoundedDbReads()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var (_, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory);

		await using var scope = _factory.Services.CreateAsyncScope();
		var sp = scope.ServiceProvider;
		var interceptor = new DbCommandCountInterceptor();
		var baseOptions = sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
		var countingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(baseOptions)
			.AddInterceptors(interceptor)
			.Options;
		await using var countingDb = new ApplicationDbContext(countingOptions);

		var cache = sp.GetRequiredService<IMemoryCache>();
		var hubCache = new HubUserDisplayCache(
			countingDb,
			cache,
			sp.GetRequiredService<IOptions<PerformanceOptions>>());

		interceptor.Reset();
		var first = await hubCache.GetAsync(userId);
		var afterFirst = interceptor.CommandCount;
		interceptor.Reset();
		var second = await hubCache.GetAsync(userId);

		first.Should().NotBeNull();
		second.Should().Be(first);
		afterFirst.Should().BeLessThanOrEqualTo(2);
		interceptor.CommandCount.Should().Be(0, "second lookup must be IMemoryCache hit");
	}

	/// <summary>BE-RP14-U2 — blocked peer history returns empty (moderation invariant).</summary>
	[Fact]
	public async Task BE_RP14_U2_BlockedPeer_MessageHistoryEmpty()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var publicClient = AclTestClients.CreatePublicFaceClient(_factory);
		var (tokenA, userA, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory);
		var (_, userB, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.Messages.Add(new BeDemo.Api.Models.Message
			{
				SenderId = userB,
				ReceiverId = userA,
				Content = "hello",
				SentAt = DateTime.UtcNow,
				IsMessageRequest = false,
			});
			db.UserBlocks.Add(new BeDemo.Api.Models.UserBlock { BlockerId = userA, BlockedId = userB });
			await db.SaveChangesAsync();
		}

		publicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
		var response = await publicClient.GetAsync($"/api/messages/with/{userB}");
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement[]>();
		body.Should().BeEmpty();
	}
}
