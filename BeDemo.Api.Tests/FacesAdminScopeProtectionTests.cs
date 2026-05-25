using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BeDemo.Api.Tests;

public class FacesAdminScopeProtectionTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _adminFace;

	public FacesAdminScopeProtectionTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_adminFace = AclTestClients.CreateAdminFaceClient(factory);
	}

	private async Task<int> GetAdminScopeFaceIdAsync()
	{
		var token = await AclTestClients.GetPlatformAdminTokenAsync(AclTestClients.CreateOAuthClient(_factory));
		_adminFace.DefaultRequestHeaders.Authorization =
			new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

		var doc = await _adminFace.GetFromJsonAsync<JsonElement>("/api/faces");
		doc.Should().NotBeNull();
		var faces = doc!.GetProperty("items").EnumerateArray().ToArray();
		var admin = faces.FirstOrDefault(f =>
			f.TryGetProperty("index", out var idx) &&
			string.Equals(idx.GetString(), "admin", StringComparison.OrdinalIgnoreCase));
		admin.ValueKind.Should().NotBe(JsonValueKind.Undefined);
		return admin.GetProperty("id").GetInt32();
	}

	[Fact]
	public async Task UpdateAdminScopeFace_ReturnsBadRequest()
	{
		var adminFaceId = await GetAdminScopeFaceIdAsync();
		var res = await _adminFace.PutAsJsonAsync($"/api/faces/{adminFaceId}", new { title = "Changed" });
		res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task DeleteAdminScopeFace_ReturnsBadRequest()
	{
		var adminFaceId = await GetAdminScopeFaceIdAsync();
		var res = await _adminFace.DeleteAsync($"/api/faces/{adminFaceId}");
		res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}
}
