using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services.Faces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP2 edge cases (BE-RP2-U1…U6).</summary>
public sealed class BeRp2FacesConfigEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp2FacesConfigEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP2-U1 — anonymous public tenant returns only public faces; cache key distinct from member.</summary>
	[Fact]
	public async Task BE_RP2_U1_AnonymousPublicTenant_OnlyPublicFaces_DistinctCacheKey()
	{
		using var anonClient = _factory.CreateFaceClient("public");
		var anonConfig = await anonClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		anonConfig.Should().NotBeNull();
		var anonIndices = anonConfig!.Select(f => f.GetProperty("index").GetString()).ToList();
		anonIndices.Should().Contain("public");
		anonIndices.Should().NotContain("basic");
		anonIndices.Should().NotContain("koncept");
		foreach (var face in anonConfig)
			face.GetProperty("isPublic").GetBoolean().Should().BeTrue();

		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory);
		using var memberClient = _factory.CreateFaceClient("public");
		memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		(await memberClient.GetAsync("/api/faces/config")).EnsureSuccessStatusCode();

		await using var scope = _factory.Services.CreateAsyncScope();
		var memory = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
		var publicFaceId = anonConfig!.First(f => f.GetProperty("index").GetString() == "public")
			.GetProperty("id").GetInt32();
		var gen = memory.Get<int?>("faces-config-gen") ?? 0;
		memory.TryGetValue($"faces-config:{gen}:False:True:{publicFaceId}:anon", out _).Should().BeTrue();
		memory.TryGetValue($"faces-config:{gen}:False:True:{publicFaceId}:{userId}", out _).Should().BeTrue();
	}

	/// <summary>BE-RP2-U2 — authenticated member with face role sees permitted private face.</summary>
	[Fact]
	public async Task BE_RP2_U2_MemberWithFaceRole_IncludesPrivateFace()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory);
		using var basicClient = _factory.CreateFaceClient("basic");
		basicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var basicFaceId = await GetFaceIdByIndexAsync(basicClient, "basic");
		var roles = await basicClient.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
		var userRoleId = roles!.First(r =>
				(r.GetProperty("name").GetString() ?? "").Contains("FACE_USER", StringComparison.OrdinalIgnoreCase))
			.GetProperty("id").GetInt32();
		(await basicClient.PutAsJsonAsync($"/api/faces/{basicFaceId}/my-role", new { userRoleId }))
			.EnsureSuccessStatusCode();

		using var publicClient = _factory.CreateFaceClient("public");
		publicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var config = await publicClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		config!.Select(f => f.GetProperty("index").GetString()).Should().Contain("basic");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		(await db.UserFaceRoles.AnyAsync(ufr => ufr.UserId == userId && ufr.FaceId == basicFaceId)).Should().BeTrue();
	}

	/// <summary>BE-RP2-U3 — admin scope requires super-admin; global admin gets Forbid.</summary>
	[Fact]
	public async Task BE_RP2_U3_AdminScope_SuperAdminFullGraph_GlobalAdminForbid()
	{
		using var adminClient = _factory.CreateFaceClient("admin");
		var globalAdminToken = await AclTestClients.GetGlobalAdminTokenAsync(AclTestClients.CreateOAuthClient(_factory));
		adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", globalAdminToken);
		(await adminClient.GetAsync("/api/faces/config")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

		var superToken = await AclTestClients.GetPlatformSuperAdminTokenAsync(AclTestClients.CreateOAuthClient(_factory));
		adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superToken);
		var superConfig = await adminClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		superConfig!.Select(f => f.GetProperty("index").GetString()).Should().Contain("admin");
	}

	/// <summary>BE-RP2-U4 — cache invalidation after generation bump reflects DB change on next GET.</summary>
	[Fact]
	public async Task BE_RP2_U4_CacheInvalidation_NextGetReflectsUpdatedPagePath()
	{
		using var publicClient = _factory.CreateFaceClient("public");
		var first = await publicClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		var publicFace = first!.First(f => f.GetProperty("index").GetString() == "public");
		var pages = publicFace.GetProperty("pages");
		if (pages.GetArrayLength() == 0)
			return;

		var pageId = pages[0].GetProperty("id").GetInt32();
		var originalPath = pages[0].GetProperty("path").GetString();
		var updatedPath = $"/rp2-cache-{Guid.NewGuid():N}";

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var page = await db.Pages.FirstAsync(p => p.Id == pageId);
			page.Path = updatedPath;
			await db.SaveChangesAsync();
			scope.ServiceProvider.GetRequiredService<IFacesConfigService>().InvalidateAll();
		}

		var second = await publicClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		var refreshedPage = second!
			.First(f => f.GetProperty("index").GetString() == "public")
			.GetProperty("pages")
			.EnumerateArray()
			.First(p => p.GetProperty("id").GetInt32() == pageId);
		refreshedPage.GetProperty("path").GetString().Should().Be(updatedPath);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var page = await db.Pages.FirstAsync(p => p.Id == pageId);
			page.Path = originalPath!;
			await db.SaveChangesAsync();
			scope.ServiceProvider.GetRequiredService<IFacesConfigService>().InvalidateAll();
		}
	}

	/// <summary>BE-RP2-U5 — scoped private tenant returns single face only.</summary>
	[Fact]
	public async Task BE_RP2_U5_ScopedPrivateTenant_SingleFaceOnly()
	{
		using var basicClient = _factory.CreateFaceClient("basic");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(basicClient);
		basicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var config = await basicClient.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		config.Should().NotBeNull();
		config!.Should().HaveCount(1);
		config[0].GetProperty("index").GetString().Should().Be("basic");
	}

	/// <summary>BE-RP2-U6 — warm cache response byte size matches repeat GET (no payload bloat).</summary>
	[Fact]
	public async Task BE_RP2_U6_WarmCache_ResponseByteSizeStable()
	{
		using var publicClient = _factory.CreateFaceClient("public");
		var coldBytes = await publicClient.GetByteArrayAsync("/api/faces/config");
		var warmBytes = await publicClient.GetByteArrayAsync("/api/faces/config");
		warmBytes.Length.Should().Be(coldBytes.Length);
		warmBytes.Length.Should().BeGreaterThan(10);
	}

	private static async Task<int> GetFaceIdByIndexAsync(HttpClient client, string index)
	{
		var cfg = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		foreach (var f in cfg!)
		{
			if (string.Equals(f.GetProperty("index").GetString(), index, StringComparison.OrdinalIgnoreCase))
				return f.GetProperty("id").GetInt32();
		}

		throw new InvalidOperationException($"Face '{index}' not found");
	}
}
