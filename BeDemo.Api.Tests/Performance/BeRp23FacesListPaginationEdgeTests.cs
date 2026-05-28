using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests.Performance;

/// <summary>BE-RP23 edge cases (BE-RP23-U1…U2) — faces list pagination envelope.</summary>
public sealed class BeRp23FacesListPaginationEdgeTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public BeRp23FacesListPaginationEdgeTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	/// <summary>BE-RP23-U1 — GET /api/faces returns paginated envelope with items array.</summary>
	[Fact]
	public async Task BE_RP23_U1_FacesList_ReturnsPaginatedEnvelope()
	{
		using var admin = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(admin);
		admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await admin.GetAsync("/api/faces?page=1&pageSize=5");
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.TryGetProperty("items", out var items).Should().BeTrue();
		items.ValueKind.Should().Be(JsonValueKind.Array);
		body.TryGetProperty("totalCount", out _).Should().BeTrue();
		body.TryGetProperty("totalPages", out _).Should().BeTrue();
	}

	/// <summary>BE-RP23-U2 — pagination totalCount matches tenant-scoped single face on basic prefix.</summary>
	[Fact]
	public async Task BE_RP23_U2_TenantScope_TotalCountIsOne()
	{
		using var basic = _factory.CreateFaceClient("basic");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(basic);
		basic.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var body = await basic.GetFromJsonAsync<JsonElement>("/api/faces?page=1&pageSize=10");
		body.GetProperty("totalCount").GetInt32().Should().Be(1);
		body.GetProperty("items").GetArrayLength().Should().Be(1);
	}
}
