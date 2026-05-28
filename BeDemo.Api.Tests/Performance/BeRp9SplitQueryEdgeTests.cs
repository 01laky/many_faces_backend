using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Services.Faces;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP9 edge cases (BE-RP9-U1…U2).</summary>
public sealed class BeRp9SplitQueryEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp9SplitQueryEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP9-U1 — faces config JSON envelope shape stable (index, id, pages, pageType).</summary>
	[Fact]
	public async Task BE_RP9_U1_FacesConfig_JsonShapeStable()
	{
		using var client = _factory.CreateFaceClient("public");
		var config = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		config.Should().NotBeNull().And.NotBeEmpty();

		foreach (var face in config!)
		{
			face.TryGetProperty("index", out _).Should().BeTrue();
			face.TryGetProperty("id", out _).Should().BeTrue();
			face.TryGetProperty("pages", out var pages).Should().BeTrue();
			pages.ValueKind.Should().Be(JsonValueKind.Array);
			if (pages.GetArrayLength() > 0)
			{
				var page = pages[0];
				page.TryGetProperty("path", out _).Should().BeTrue();
				page.TryGetProperty("pageType", out _).Should().BeTrue();
			}
		}
	}

	/// <summary>BE-RP9-U2 — video lounge list member counts correct on seeded public face.</summary>
	[Fact]
	public async Task BE_RP9_U2_VideoLoungeList_MemberCountsCorrect()
	{
		using var client = _factory.CreateFaceClient("public");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

		var faceId = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		var publicId = faceId!.First(f => f.GetProperty("index").GetString() == "public").GetProperty("id").GetInt32();

		var response = await client.GetAsync($"/api/faces/{publicId}/video-lounges?page=1&pageSize=20");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.TryGetProperty("items", out var items).Should().BeTrue();
		foreach (var lounge in items.EnumerateArray())
		{
			lounge.TryGetProperty("memberCount", out var countProp).Should().BeTrue();
			countProp.GetInt32().Should().BeGreaterThanOrEqualTo(0);
		}
	}

	/// <summary>BE-RP9-U1b — AsSplitQuery path does not explode command count on faces config load.</summary>
	[Fact]
	public async Task BE_RP9_U1b_FacesConfigLoad_BoundedCommandCount()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var sp = scope.ServiceProvider;
		var interceptor = new DbCommandCountInterceptor();
		var baseOptions = sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
		var countingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(baseOptions)
			.AddInterceptors(interceptor)
			.Options;
		await using var countingDb = new ApplicationDbContext(countingOptions);

		var svc = new FacesConfigService(
			countingDb,
			new PerformanceTestFaceScope(),
			sp.GetRequiredService<BeDemo.Api.Services.IAccessEvaluator>(),
			sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
			sp.GetRequiredService<IOptions<PerformanceOptions>>(),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<FacesConfigService>.Instance);

		interceptor.Reset();
		await svc.GetFacesConfigAsync(new System.Security.Claims.ClaimsPrincipal(), null);
		interceptor.CommandCount.Should().BeLessThanOrEqualTo(12,
			"AsSplitQuery should bound faces+pages load without cartesian explosion");
	}
}
