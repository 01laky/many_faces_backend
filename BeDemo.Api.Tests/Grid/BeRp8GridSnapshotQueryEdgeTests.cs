using System.Net.Http.Json;
using System.Net.Http.Headers;
using BeDemo.Api.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Services;
using BeDemo.Api.Services.Grid;
using BeDemo.Api.Utils;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests.Grid;

/// <summary>BE-RP8-U5 — grid snapshot query count vs standalone block fetches.</summary>
public sealed class BeRp8GridSnapshotQueryEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp8GridSnapshotQueryEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task BE_RP8_U5_SnapshotQueryCount_NotGreaterThanStandaloneSum()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var sp = scope.ServiceProvider;
		var options = sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
		var interceptor = new BeDemo.Api.Tests.Performance.DbCommandCountInterceptor();
		var countingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
			.AddInterceptors(interceptor)
			.Options;
		await using var countingDb = new ApplicationDbContext(countingOptions);

		var faceId = await countingDb.Faces.AsNoTracking()
			.Where(f => f.Index == "public")
			.Select(f => f.Id)
			.FirstAsync();

		var perf = sp.GetRequiredService<IOptions<PerformanceOptions>>();
		var faceScope = new BeDemo.Api.Tests.Performance.PerformanceTestFaceScope();
		var access = sp.GetRequiredService<IAccessEvaluator>();
		var uploadUrls = sp.GetRequiredService<IUploadSignedUrlService>();

		var albums = new AlbumGridListService(countingDb, faceScope, access, perf);
		var blogs = new BlogGridListService(countingDb, faceScope, access, perf);
		var snapshot = new FaceGridSnapshotService(
			countingDb,
			faceScope,
			access,
			uploadUrls,
			albums,
			blogs,
			new ReelGridListService(countingDb, faceScope, access, perf),
			new StoryGridListService(countingDb, faceScope, access, perf),
			perf);

		interceptor.Reset();
		_ = await snapshot.GetSnapshotAsync(
			faceId,
			new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()),
			null,
			[GridBlockKeys.Albums, GridBlockKeys.Blogs],
			1,
			10,
			"http",
			"localhost",
			CancellationToken.None);
		var snapshotCommands = interceptor.CommandCount;

		interceptor.Reset();
		_ = await albums.GetAlbumsAsync(
			new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()),
			null,
			new BeDemo.Api.Models.Requests.Albums.AlbumListQuery { FaceId = faceId, Page = 1, PageSize = 10 },
			CancellationToken.None);
		var albumsCommands = interceptor.CommandCount;

		interceptor.Reset();
		_ = await blogs.GetBlogsAsync(
			new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()),
			null,
			new BeDemo.Api.Models.Requests.Blogs.BlogListQuery { FaceId = faceId, Page = 1, PageSize = 10 },
			CancellationToken.None);
		var blogsCommands = interceptor.CommandCount;

		snapshotCommands.Should().BeLessThanOrEqualTo(albumsCommands + blogsCommands + 2,
			"snapshot should reuse shared services without extra round-trips per block");
	}

	/// <summary>BE-RP8-U4 — duplicate block keys in query are deduped.</summary>
	[Fact]
	public async Task BE_RP8_U4_DuplicateBlockKeys_DedupedInResponse()
	{
		using var client = _factory.CreateFaceClient("public");
		var faceId = await GetPublicFaceIdAsync(client);
		var snapshot = await client.GetFromJsonAsync<System.Text.Json.JsonElement>(
			$"/api/faces/{faceId}/grid-snapshot?blocks={GridBlockKeys.Profiles},{GridBlockKeys.Profiles}&page=1&pageSize=5");
		snapshot!.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo([GridBlockKeys.Profiles]);
	}

	private static async Task<int> GetPublicFaceIdAsync(HttpClient client)
	{
		var cfg = await client.GetFromJsonAsync<System.Text.Json.JsonElement[]>("/api/faces/config");
		return cfg!.First(f => f.GetProperty("index").GetString() == "public").GetProperty("id").GetInt32();
	}
}
