using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using BeDemo.Api.Services.Grid;

namespace BeDemo.Api.Tests.Grid;

/// <summary>BE-RP34 — grid snapshot contract tests vs individual list endpoints and golden fixtures.</summary>
public sealed class FaceGridSnapshotContractTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private const string Password = "Test1234!@##";
	private const int Page = 1;
	private const int PageSize = 10;

	private static readonly string AllBlocksQuery = string.Join(',',
		GridBlockKeys.Albums,
		GridBlockKeys.Blogs,
		GridBlockKeys.Reels,
		GridBlockKeys.Stories,
		GridBlockKeys.ChatRooms,
		GridBlockKeys.Profiles,
		GridBlockKeys.WallTickets);

	private readonly CustomWebApplicationFactory<Program> _factory;

	public FaceGridSnapshotContractTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP34-U1 — six block types; snapshot sections deep-equal standalone list endpoints and golden file.</summary>
	[Fact]
	public async Task BE_RP34_U1_SixBlockTypes_MatchStandaloneEndpointsAndGolden()
	{
		using var client = _factory.CreateFaceClient("public");
		var (token, _) = await RegisterMemberAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");

		var standalone = await FetchStandaloneBlocksAsync(client, faceId);
		var snapshot = await FetchSnapshotAsync(client, faceId, AllBlocksQuery);

		foreach (var (key, expected) in standalone)
		{
			snapshot.TryGetProperty(key, out var actual).Should().BeTrue($"snapshot missing block '{key}'");
			GridJsonComparer.DeepEquals(
				GridJsonComparer.Parse(actual.GetRawText()),
				GridJsonComparer.Parse(expected.GetRawText()),
				out var diff).Should().BeTrue(diff);
		}

		Assert.True(File.Exists(GridGoldenFixturePaths.MemberSnapshot),
			$"Golden file missing at {GridGoldenFixturePaths.MemberSnapshot}");
		var golden = GridJsonComparer.Parse(await File.ReadAllTextAsync(GridGoldenFixturePaths.MemberSnapshot));
		var actualNode = GridJsonComparer.Parse(snapshot.GetRawText());
		foreach (var block in golden.AsObject())
		{
			actualNode[block.Key].Should().NotBeNull($"snapshot missing golden block '{block.Key}'");
			if (string.Equals(block.Key, GridBlockKeys.Profiles, StringComparison.OrdinalIgnoreCase))
			{
				GridGoldenSchemaAssert.ProfilesEnvelopeMatchesSchema(
					snapshot.GetProperty(GridBlockKeys.Profiles),
					block.Value!);
				continue;
			}

			GridGoldenSchemaAssert.PaginatedEnvelopeMatchesSchema(actualNode[block.Key]!);
		}
	}

	/// <summary>BE-RP34-U2 — guest vs member ACL for profiles block matches individual endpoint.</summary>
	[Fact]
	public async Task BE_RP34_U2_GuestAndMemberProfiles_AclMatchesStandaloneEndpoint()
	{
		using var guestClient = _factory.CreateFaceClient("public");
		var faceId = await GetPublicFaceIdAnonymouslyAsync(guestClient);

		var guestStandalone = await guestClient.GetAsync($"/api/faces/{faceId}/profiles?page={Page}&pageSize={PageSize}");
		guestStandalone.StatusCode.Should().Be(HttpStatusCode.OK);
		var guestStandaloneJson = await guestStandalone.Content.ReadFromJsonAsync<JsonElement>();

		var guestSnapshot = await FetchSnapshotAsync(guestClient, faceId, GridBlockKeys.Profiles);
		guestSnapshot.TryGetProperty(GridBlockKeys.Profiles, out var guestProfiles).Should().BeTrue();
		GridJsonComparer.DeepEquals(
			GridJsonComparer.Parse(guestProfiles.GetRawText()),
			GridJsonComparer.Parse(guestStandaloneJson.GetRawText()),
			out var guestDiff).Should().BeTrue(guestDiff);

		Assert.True(File.Exists(GridGoldenFixturePaths.GuestProfilesSnapshot),
			$"Golden file missing at {GridGoldenFixturePaths.GuestProfilesSnapshot}");
		var guestGolden = GridJsonComparer.Parse(await File.ReadAllTextAsync(GridGoldenFixturePaths.GuestProfilesSnapshot));
		GridGoldenSchemaAssert.ProfilesEnvelopeMatchesSchema(guestProfiles, guestGolden);

		using var memberClient = _factory.CreateFaceClient("public");
		var (token, _) = await RegisterMemberAsync(memberClient);
		memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var memberFaceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(memberClient, token, "public");

		var memberStandalone = await memberClient.GetFromJsonAsync<JsonElement>(
			$"/api/faces/{memberFaceId}/profiles?page={Page}&pageSize={PageSize}");
		var memberSnapshot = await FetchSnapshotAsync(memberClient, memberFaceId, GridBlockKeys.Profiles);
		memberSnapshot.TryGetProperty(GridBlockKeys.Profiles, out var memberProfiles).Should().BeTrue();
		GridJsonComparer.DeepEquals(
			GridJsonComparer.Parse(memberProfiles.GetRawText()),
			GridJsonComparer.Parse(memberStandalone!.GetRawText()),
			out var memberDiff).Should().BeTrue(memberDiff);

		memberProfiles.GetRawText().Should().NotBe(guestProfiles.GetRawText(),
			"member and guest profile directory ACL should produce distinguishable payloads on seeded data");
	}

	/// <summary>BE-RP34-U3 — unknown block keys ignored (BE-RP8-U2); valid blocks still match standalone endpoints.</summary>
	[Fact]
	public async Task BE_RP34_U3_UnknownBlockKeys_IgnoredValidBlocksMatchStandalone()
	{
		using var client = _factory.CreateFaceClient("public");
		var (token, _) = await RegisterMemberAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");

		var blocksQuery = $"{GridBlockKeys.Albums},unknown-block,{GridBlockKeys.Blogs},{GridBlockKeys.Albums}";
		var snapshot = await FetchSnapshotAsync(client, faceId, blocksQuery);

		snapshot.TryGetProperty("unknown-block", out _).Should().BeFalse();
		snapshot.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
			[GridBlockKeys.Albums, GridBlockKeys.Blogs],
			opts => opts.WithStrictOrdering());

		var albumsStandalone = await client.GetFromJsonAsync<JsonElement>(
			$"/api/albums?faceId={faceId}&page={Page}&pageSize={PageSize}");
		snapshot.TryGetProperty(GridBlockKeys.Albums, out var albumsBlock).Should().BeTrue();
		GridJsonComparer.DeepEquals(
			GridJsonComparer.Parse(albumsBlock.GetRawText()),
			GridJsonComparer.Parse(albumsStandalone!.GetRawText()),
			out var albumsDiff).Should().BeTrue(albumsDiff);

		var blogsStandalone = await client.GetFromJsonAsync<JsonElement>(
			$"/api/blogs?faceId={faceId}&page={Page}&pageSize={PageSize}");
		snapshot.TryGetProperty(GridBlockKeys.Blogs, out var blogsBlock).Should().BeTrue();
		GridJsonComparer.DeepEquals(
			GridJsonComparer.Parse(blogsBlock.GetRawText()),
			GridJsonComparer.Parse(blogsStandalone!.GetRawText()),
			out var blogsDiff).Should().BeTrue(blogsDiff);
	}

	[Fact]
	public async Task BE_RP34_U3_AllUnknownBlocks_Returns400()
	{
		using var client = _factory.CreateFaceClient("public");
		var (token, _) = await RegisterMemberAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");

		var response = await client.GetAsync(
			$"/api/faces/{faceId}/grid-snapshot?blocks=not-real,also-invalid&page={Page}&pageSize={PageSize}");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	/// <summary>Operator-only: rewrite golden fixtures when schema changes are intentional.</summary>
	[Fact]
	public async Task RegenerateGridGoldenFixtures()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("REGENERATE_GRID_GOLDEN"), "1", StringComparison.Ordinal))
			return;

		using var goldenClient = _factory.CreateFaceClient("public");
		var goldenToken = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(goldenClient);
		goldenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", goldenToken);
		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(goldenClient, goldenToken, "public");
		var memberSnapshot = await FetchSnapshotAsync(goldenClient, faceId, AllBlocksQuery);
		await WriteGoldenAsync(
			Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "Grid", "grid-snapshot-member-public.json"),
			memberSnapshot);

		using var goldenGuestClient = _factory.CreateFaceClient("public");
		var goldenGuestFaceId = await GetPublicFaceIdAnonymouslyAsync(goldenGuestClient);
		var goldenGuestSnapshot = await FetchSnapshotAsync(goldenGuestClient, goldenGuestFaceId, GridBlockKeys.Profiles);
		goldenGuestSnapshot.TryGetProperty(GridBlockKeys.Profiles, out var guestProfilesBlock).Should().BeTrue();
		await WriteGoldenAsync(
			Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "Grid", "grid-snapshot-guest-profiles-public.json"),
			guestProfilesBlock);
	}

	private static async Task WriteGoldenAsync(string repoRelativeFromOutput, JsonElement payload)
	{
		var path = Path.GetFullPath(repoRelativeFromOutput);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var canonical = GridJsonComparer.ToCanonicalJson(GridJsonComparer.Parse(payload.GetRawText()));
		await File.WriteAllTextAsync(path, canonical.TrimEnd() + Environment.NewLine);
	}

	private async Task<(string Token, string UserId)> RegisterMemberAsync(HttpClient client)
	{
		var (token, userId, _) = await IntegrationTestRegistration.RegisterLoginWithUserIdAsync(
			client,
			_factory,
			$"grid_{Guid.NewGuid():N}@test.com",
			Password,
			"Grid",
			"Member");

		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var faceId = await IntegrationTestFaceHelper.GetScopedFaceIdFromConfigAsync(client, token, "public");
		var roles = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/face-roles");
		var userRoleId = roles!.First(r =>
			(r.GetProperty("name").GetString() ?? "").Contains("FACE_USER", StringComparison.OrdinalIgnoreCase))
			.GetProperty("id").GetInt32();
		(await client.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId })).EnsureSuccessStatusCode();
		return (token, userId);
	}

	private static async Task<int> GetPublicFaceIdAnonymouslyAsync(HttpClient client)
	{
		var cfg = await client.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		foreach (var f in cfg!)
		{
			if (string.Equals(f.GetProperty("index").GetString(), "public", StringComparison.OrdinalIgnoreCase))
				return f.GetProperty("id").GetInt32();
		}

		throw new InvalidOperationException("public face not found");
	}

	private static async Task<Dictionary<string, JsonElement>> FetchStandaloneBlocksAsync(HttpClient client, int faceId)
	{
		var qs = $"?faceId={faceId}&page={Page}&pageSize={PageSize}";
		return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
		{
			[GridBlockKeys.Albums] = (await client.GetFromJsonAsync<JsonElement>($"/api/albums{qs}"))!,
			[GridBlockKeys.Blogs] = (await client.GetFromJsonAsync<JsonElement>($"/api/blogs{qs}"))!,
			[GridBlockKeys.Reels] = (await client.GetFromJsonAsync<JsonElement>($"/api/reels{qs}"))!,
			[GridBlockKeys.Stories] = (await client.GetFromJsonAsync<JsonElement>($"/api/stories{qs}"))!,
			[GridBlockKeys.ChatRooms] = (await client.GetFromJsonAsync<JsonElement>(
				$"/api/faces/{faceId}/chat-rooms?page={Page}&pageSize={PageSize}"))!,
			[GridBlockKeys.Profiles] = (await client.GetFromJsonAsync<JsonElement>(
				$"/api/faces/{faceId}/profiles?page={Page}&pageSize={PageSize}"))!,
			[GridBlockKeys.WallTickets] = (await client.GetFromJsonAsync<JsonElement>(
				$"/api/faces/{faceId}/wall-tickets?page={Page}&pageSize={PageSize}"))!,
		};
	}

	private static async Task<JsonElement> FetchSnapshotAsync(HttpClient client, int faceId, string blocks) =>
		(await client.GetFromJsonAsync<JsonElement>(
			$"/api/faces/{faceId}/grid-snapshot?blocks={blocks}&page={Page}&pageSize={PageSize}"))!;
}
