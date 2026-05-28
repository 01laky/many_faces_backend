using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP7 edge cases (BE-RP7-U1…U2).</summary>
public sealed class BeRp7FaceRoutingCacheEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp7FaceRoutingCacheEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP7-U1 — routing middleware populates Faces cache on first face-scoped request.</summary>
	[Fact]
	public async Task BE_RP7_U1_CacheMissThenHit_SecondRequestUsesFacesCache()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var memory = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
		memory.TryGetValue("Faces", out _).Should().BeFalse();

		using var client = _factory.CreateFaceClient("public");
		(await client.GetAsync("/api/faces/config")).EnsureSuccessStatusCode();
		memory.TryGetValue("Faces", out IList<BeDemo.Api.Models.Face>? cached).Should().BeTrue();
		cached.Should().NotBeNull().And.NotBeEmpty();

		(await client.GetAsync("/api/Stats/public")).EnsureSuccessStatusCode();
		memory.TryGetValue("Faces", out IList<BeDemo.Api.Models.Face>? stillCached).Should().BeTrue();
		stillCached!.Count.Should().Be(cached!.Count);
	}

	/// <summary>BE-RP7-U2 — face CRUD invalidates Faces routing cache key.</summary>
	[Fact]
	public async Task BE_RP7_U2_FaceCreate_InvalidatesFacesCacheKey()
	{
		using var publicClient = _factory.CreateFaceClient("public");
		(await publicClient.GetAsync("/api/faces/config")).EnsureSuccessStatusCode();

		await using var scope = _factory.Services.CreateAsyncScope();
		var memory = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
		memory.TryGetValue("Faces", out _).Should().BeTrue();

		using var admin = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
		admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var index = $"rp7_{Guid.NewGuid():N}";
		(await admin.PostAsJsonAsync("/api/faces", new
		{
			index,
			title = "RP7 invalidation probe",
			description = "cache bust",
		})).EnsureSuccessStatusCode();

		memory.TryGetValue("Faces", out _).Should().BeFalse("InvalidateFacesRoutingCache removes Faces key on CRUD");
	}
}
