using System.Net;
using System.Net.Http.Headers;
using BeDemo.Api.Data;
using BeDemo.Api.Services.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP1 edge cases (BE-RP1-U1…U5).</summary>
public sealed class BeRp1AccessTokenVersionCacheEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp1AccessTokenVersionCacheEdgeTests(CustomWebApplicationFactory<Program> factory) =>
		_factory = factory;

	/// <summary>BE-RP1-U1 — valid token with matching atv succeeds on authenticated route.</summary>
	[Fact]
	public async Task BE_RP1_U1_ValidTokenWithMatchingAtv_RequestSucceeds()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, oauth);
		var client = _factory.CreateUnscopedClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/profile/me");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	/// <summary>BE-RP1-U2 — revoked session (version bumped) fails on next request.</summary>
	[Fact]
	public async Task BE_RP1_U2_RevokedSession_NextRequestUnauthorized()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var email = $"atv_rp1_{Guid.NewGuid():N}@test.com";
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, oauth, email);
		var client = AclTestClients.CreatePublicFaceClient(_factory);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		(await client.GetAsync("/api/me/capabilities")).StatusCode.Should().Be(HttpStatusCode.OK);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var user = await db.Users.FirstAsync(u => u.Email == email);
			user.AccessTokenVersion++;
			await db.SaveChangesAsync();
		}

		(await client.GetAsync("/api/me/capabilities")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>BE-RP1-U3 — session bump clears ATV cache entry (interceptor on SaveChanges).</summary>
	[Fact]
	public async Task BE_RP1_U3_SessionBump_InvalidatesCacheEntry()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var email = $"atv_inv_{Guid.NewGuid():N}@test.com";
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, oauth, email);
		string userId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			userId = (await db.Users.AsNoTracking().FirstAsync(u => u.Email == email)).Id;
		}

		await using var queryScope = _factory.Services.CreateAsyncScope();
		var atvCache = queryScope.ServiceProvider.GetRequiredService<IAccessTokenVersionCache>();
		var memory = queryScope.ServiceProvider.GetRequiredService<IMemoryCache>();

		await atvCache.GetVersionAsync(userId);
		memory.TryGetValue($"atv:{userId}", out _).Should().BeTrue();

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var user = await db.Users.FirstAsync(u => u.Id == userId);
			user.AccessTokenVersion++;
			await db.SaveChangesAsync();
		}

		memory.TryGetValue($"atv:{userId}", out _).Should().BeFalse();

		var client = AclTestClients.CreatePublicFaceClient(_factory);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		(await client.GetAsync("/api/me/capabilities")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>BE-RP1-U4 — missing user returns null from cache service.</summary>
	[Fact]
	public async Task BE_RP1_U4_MissingUser_GetVersionReturnsNull()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var atvCache = scope.ServiceProvider.GetRequiredService<IAccessTokenVersionCache>();
		var result = await atvCache.GetVersionAsync(Guid.NewGuid().ToString("N"));
		result.Should().BeNull();
	}

	/// <summary>BE-RP1-U5 — cache miss populates once; second call hits IMemoryCache.</summary>
	[Fact]
	public async Task BE_RP1_U5_CacheMissThenHit_SecondCallUsesMemoryCache()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var email = $"atv_cache_{Guid.NewGuid():N}@test.com";
		await AclTestClients.RegisterAndGetTokenAsync(_factory, oauth, email);
		string userId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			userId = (await db.Users.AsNoTracking().FirstAsync(u => u.Email == email)).Id;
		}

		await using var queryScope = _factory.Services.CreateAsyncScope();
		var atvCache = queryScope.ServiceProvider.GetRequiredService<IAccessTokenVersionCache>();
		var memory = queryScope.ServiceProvider.GetRequiredService<IMemoryCache>();

		memory.TryGetValue($"atv:{userId}", out _).Should().BeFalse();
		var first = await atvCache.GetVersionAsync(userId);
		first.Should().NotBeNull();
		memory.TryGetValue($"atv:{userId}", out int cached).Should().BeTrue();
		cached.Should().Be(first!.Value);

		var second = await atvCache.GetVersionAsync(userId);
		second.Should().Be(first);
	}
}
