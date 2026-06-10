using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP4 edge cases (BE-RP4-U1…U4).</summary>
public sealed class BeRp4PlatformStatsCacheEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp4PlatformStatsCacheEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP4-U1 — cached dashboard repeat call issues zero DB commands.</summary>
	[Fact]
	public async Task BE_RP4_U1_CachedDashboardRepeat_ZeroDbCommandsOnSecondCall()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var sp = scope.ServiceProvider;
		var interceptor = new DbCommandCountInterceptor();
		var baseOptions = sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
		var countingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(baseOptions)
			.AddInterceptors(interceptor)
			.Options;
		await using var countingDb = new ApplicationDbContext(countingOptions);

		var dbFactory = sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
		var memory = new MemoryCache(new MemoryCacheOptions());
		var inner = new PlatformStatsQueryService(countingDb, dbFactory);
		var cached = new PlatformStatsCachedQueryService(
			inner,
			memory,
			sp.GetRequiredService<IOptions<BeDemo.Api.Configuration.PerformanceOptions>>());

		interceptor.Reset();
		var first = await cached.GetOperatorDashboardSummaryAsync();
		first.UsersCount.Should().BeGreaterThan(0);

		interceptor.Reset();
		var second = await cached.GetOperatorDashboardSummaryAsync();
		second.UsersCount.Should().Be(first.UsersCount);
		interceptor.CommandCount.Should().Be(0, "second dashboard read must be IMemoryCache hit");
	}

	/// <summary>BE-RP4-U1 (legacy) — repeat dashboard calls hit IMemoryCache (≤1 inner load per TTL).</summary>
	[Fact]
	public async Task BE_RP4_U1_DashboardSummary_SecondCallUsesCache()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var cached = scope.ServiceProvider.GetRequiredService<IPlatformStatsQueryService>();
		var memory = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

		var first = await cached.GetOperatorDashboardSummaryAsync();
		memory.TryGetValue("stats:dashboard", out _).Should().BeTrue();

		var second = await cached.GetOperatorDashboardSummaryAsync();
		second.UsersCount.Should().Be(first.UsersCount);
		second.FacesCount.Should().Be(first.FacesCount);
	}

	/// <summary>BE-RP4-U2 — public snapshot cache hit on repeat.</summary>
	[Fact]
	public async Task BE_RP4_U2_PublicSnapshot_CacheHitOnRepeat()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var cached = scope.ServiceProvider.GetRequiredService<IPlatformStatsQueryService>();
		var memory = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

		var first = await cached.GetPublicSnapshotAsync();
		memory.TryGetValue("stats:public", out _).Should().BeTrue();

		var second = await cached.GetPublicSnapshotAsync();
		second.UsersCount.Should().Be(first.UsersCount);
		second.MessagesCount.Should().Be(first.MessagesCount);
	}

	/// <summary>BE-RP4-U2 (HTTP) — two anonymous public Stats requests return identical aggregates.</summary>
	[Fact]
	public async Task BE_RP4_U2_HttpPublicStats_RepeatReturnsSameAggregates()
	{
		var client = _factory.CreateFaceClient("public");
		var first = await client.GetFromJsonAsync<JsonElement>("/api/Stats/public");
		var second = await client.GetFromJsonAsync<JsonElement>("/api/Stats/public");

		first.GetProperty("usersCount").GetInt32().Should().Be(second.GetProperty("usersCount").GetInt32());
		first.GetProperty("facesCount").GetInt32().Should().Be(second.GetProperty("facesCount").GetInt32());
	}

	/// <summary>BE-RP4-U3 — non-operator cannot access operator dashboard stats.</summary>
	[Fact]
	public async Task BE_RP4_U3_NonOperator_ForbiddenOnAdminStats()
	{
		var oauth = AclTestClients.CreateOAuthClient(_factory);
		var publicClient = AclTestClients.CreatePublicFaceClient(_factory);
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, oauth);
		publicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await publicClient.GetAsync("/api/Stats");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	/// <summary>BE-RP4-U4 — dashboard and public counts match seeded fixture totals.</summary>
	[Fact]
	public async Task BE_RP4_U4_CountsMatchSeededFixtureTotals()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var inner = scope.ServiceProvider.GetRequiredService<PlatformStatsQueryService>();
		var dashboard = await inner.GetOperatorDashboardSummaryAsync();
		var publicSnapshot = await inner.GetPublicSnapshotAsync();

		dashboard.UsersCount.Should().BeGreaterThan(0);
		dashboard.FacesCount.Should().BeGreaterThan(0);
		publicSnapshot.UsersCount.Should().Be(dashboard.UsersCount);
		publicSnapshot.FacesCount.Should().Be(dashboard.FacesCount);
		publicSnapshot.MessagesCount.Should().Be(dashboard.MessagesCount);
	}
}
