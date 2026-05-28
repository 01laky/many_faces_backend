using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeDemo.Api.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>
/// BE-RP11 edge cases (BE-RP11-U1…U2). Compiled queries not shipped — dynamic hot-path reads serve as parity baseline.
/// </summary>
public sealed class BeRp11CompiledQueryEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp11CompiledQueryEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP11-U1 — face-by-id API matches direct EF read (future compiled query must match this).</summary>
	[Fact]
	public async Task BE_RP11_U1_FaceById_MatchesDynamicEfQuery()
	{
		int faceId;
		string expectedIndex;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var face = await db.Faces.AsNoTracking().FirstAsync();
			faceId = face.Id;
			expectedIndex = face.Index;
		}

		using var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.GetFromJsonAsync<JsonElement>($"/api/faces/{faceId}");
		response.GetProperty("id").GetInt32().Should().Be(faceId);
		response.GetProperty("index").GetString().Should().Be(expectedIndex);
	}

	/// <summary>BE-RP11-U2 — page components by page returns correct count vs DB.</summary>
	[Fact]
	public async Task BE_RP11_U2_PageComponentsByPage_CorrectCount()
	{
		int pageId;
		int dbCount;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			if (!await db.PageComponents.AnyAsync())
				return;

			pageId = (await db.PageComponents.AsNoTracking().FirstAsync()).PageId;
			dbCount = await db.PageComponents.CountAsync(pc => pc.PageId == pageId);
		}

		using var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var items = await client.GetFromJsonAsync<JsonElement[]>($"/api/pagecomponents/page/{pageId}");
		items.Should().NotBeNull();
		items!.Length.Should().Be(dbCount);
	}
}
