using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP26 edge cases (BE-RP26-U1…U6).</summary>
public sealed class BeRp26CapabilitiesCacheEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp26CapabilitiesCacheEdgeTests(CustomWebApplicationFactory<Program> factory) =>
		_factory = factory;

	/// <summary>BE-RP26-U1 — anonymous request returns 401.</summary>
	[Fact]
	public async Task BE_RP26_U1_Anonymous_ReturnsUnauthorized()
	{
		var client = AclTestClients.CreatePublicFaceClient(_factory);
		var response = await client.GetAsync("/api/me/capabilities");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>BE-RP26-U2 — repeat GET populates IMemoryCache within TTL.</summary>
	[Fact]
	public async Task BE_RP26_U2_MemberRepeatGet_UsesCapabilitiesCache()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var email = $"cap_cache_{Guid.NewGuid():N}@test.com";
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
			oauth, _factory, email);
		var client = AclTestClients.CreatePublicFaceClient(_factory);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var first = await client.GetFromJsonAsync<CapabilitiesResponse>("/api/me/capabilities");
		first.Should().NotBeNull();

		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");
		await using var cacheScope = _factory.Services.CreateAsyncScope();
		var memory = cacheScope.ServiceProvider.GetRequiredService<IMemoryCache>();
		memory.TryGetValue($"cap:{userId}:{faceId}:False", out _).Should().BeTrue();

		var second = await client.GetFromJsonAsync<CapabilitiesResponse>("/api/me/capabilities");
		second.Should().BeEquivalentTo(first);
	}

	/// <summary>BE-RP26-U3 — global role change invalidates JWT session.</summary>
	[Fact]
	public async Task BE_RP26_U3_RoleChange_RequiresReauth()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var email = $"cap_role_{Guid.NewGuid():N}@test.com";
		var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory, email);
		var client = AclTestClients.CreatePublicFaceClient(_factory);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		(await client.GetAsync("/api/me/capabilities")).EnsureSuccessStatusCode();

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var adminRole = await db.UserRoles.AsNoTracking()
				.FirstAsync(r => r.Name == UserRole.GlobalRoleNames.Admin);
			var user = await db.Users.FirstAsync(u => u.Email == email);
			user.UserRoleId = adminRole.Id;
			await db.SaveChangesAsync();
		}

		(await client.GetAsync("/api/me/capabilities")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	/// <summary>BE-RP26-U4 — admin face scope differs from tenant scope.</summary>
	[Fact]
	public async Task BE_RP26_U4_AdminScope_DistinctFromTenantScope()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(oauth);

		var publicClient = AclTestClients.CreatePublicFaceClient(_factory);
		publicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var publicRes = await publicClient.GetFromJsonAsync<CapabilitiesResponse>("/api/me/capabilities");

		var adminClient = AclTestClients.CreateAdminFaceClient(_factory);
		adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var adminRes = await adminClient.GetFromJsonAsync<CapabilitiesResponse>("/api/me/capabilities");

		publicRes.Should().NotBeNull();
		adminRes.Should().NotBeNull();
		adminRes!.Permissions.Should().NotBeEquivalentTo(publicRes!.Permissions);
	}

	/// <summary>BE-RP26-U5 — permission list stable across cache hit.</summary>
	[Fact]
	public async Task BE_RP26_U5_PermissionList_MatchesPreCacheBaseline()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var email = $"cap_golden_{Guid.NewGuid():N}@test.com";
		var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory, email);
		var client = AclTestClients.CreatePublicFaceClient(_factory);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var cold = await client.GetFromJsonAsync<CapabilitiesResponse>("/api/me/capabilities");
		cold.Should().NotBeNull();
		cold!.Permissions.Should().NotBeEmpty();

		var warm = await client.GetFromJsonAsync<CapabilitiesResponse>("/api/me/capabilities");
		warm!.Permissions.Should().Equal(cold.Permissions);
		warm.GlobalRole.Should().Be(cold.GlobalRole);
	}

	/// <summary>BE-RP26-U6 — missing face prefix returns 400.</summary>
	[Fact]
	public async Task BE_RP26_U6_MissingFacePrefix_ReturnsBadRequest()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var (token, _, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(oauth, _factory);
		var client = _factory.CreateUnscopedClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetAsync("/api/me/capabilities");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("Face URL prefix");
	}
}
