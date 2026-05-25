/*
 * PageTypesControllerTests — global PageType schema is mutable only from admin scope + platform admin (A15).
 */

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BeDemo.Api.Tests;

public class PageTypesControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _publicFace;
	private readonly HttpClient _adminFace;

	public PageTypesControllerTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_publicFace = AclTestClients.CreatePublicFaceClient(factory);
		_adminFace = AclTestClients.CreateAdminFaceClient(factory);
	}

	[Fact]
	public async Task GetPageTypes_ShouldReturnUnauthorized_WhenNoToken()
	{
		var response = await _publicFace.GetAsync("/api/pagetypes");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetPageTypes_ShouldReturnPageTypesList_WhenAuthenticatedTenantUser()
	{
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await _publicFace.GetAsync("/api/pagetypes");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var pageTypes = await response.Content.ReadFromJsonAsync<List<object>>();
		pageTypes.Should().NotBeNull();
	}

	[Fact]
	public async Task CreatePageType_ShouldForbid_WhenTenantUserOnPublicFace()
	{
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await _publicFace.PostAsJsonAsync("/api/pagetypes", new { index = $"deny_{Guid.NewGuid():N}" });
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task CreateUpdateDeletePageType_ShouldSucceed_WhenPlatformAdminOnAdminFace()
	{
		var token = await AclTestClients.GetPlatformAdminTokenAsync(_oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var createResponse = await _adminFace.PostAsJsonAsync("/api/pagetypes", new { index = $"ok_{Guid.NewGuid():N}" });
		createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("id").GetInt32();

		var putResponse = await _adminFace.PutAsJsonAsync($"/api/pagetypes/{id}", new { index = $"upd_{Guid.NewGuid():N}" });
		putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var delResponse = await _adminFace.DeleteAsync($"/api/pagetypes/{id}");
		delResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task GetPageType_ById_ShouldWork_ForTenantUser_WithoutMutateRights()
	{
		var adminToken = await AclTestClients.GetPlatformAdminTokenAsync(_oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
		var createResponse = await _adminFace.PostAsJsonAsync("/api/pagetypes", new { index = $"read_{Guid.NewGuid():N}" });
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("id").GetInt32();

		var userToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
		var getResponse = await _publicFace.GetAsync($"/api/pagetypes/{id}");
		getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	public void Dispose()
	{
		_oauth?.Dispose();
		_publicFace?.Dispose();
		_adminFace?.Dispose();
	}
}
