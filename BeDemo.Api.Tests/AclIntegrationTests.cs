/*
 * ACL edge-case integration tests (A10, A15, A16): capabilities, face-roles listing, my-role self-service whitelist.
 */

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;

namespace BeDemo.Api.Tests;

public class AclIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _publicFace;
	private readonly HttpClient _adminFace;
	private readonly HttpClient _unscoped;

	public AclIntegrationTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_publicFace = AclTestClients.CreatePublicFaceClient(factory);
		_adminFace = AclTestClients.CreateAdminFaceClient(factory);
		_unscoped = factory.CreateUnscopedClient();
	}

	public void Dispose()
	{
		_oauth.Dispose();
		_publicFace.Dispose();
		_adminFace.Dispose();
		_unscoped.Dispose();
	}

	private static int PublicFaceIdFromConfig(JsonElement[] cfg) =>
		cfg.First(f => f.GetProperty("index").GetString() == "public").GetProperty("id").GetInt32();

	private static bool JsonArrayContainsRoleName(JsonElement arr, string name)
	{
		foreach (var el in arr.EnumerateArray())
		{
			if (el.TryGetProperty("name", out var n) && n.GetString() == name)
				return true;
		}

		return false;
	}

	private static int? JsonArrayRoleIdByName(JsonElement arr, string name)
	{
		foreach (var el in arr.EnumerateArray())
		{
			if (el.TryGetProperty("name", out var n) && n.GetString() == name &&
				el.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
				return id.GetInt32();
		}

		return null;
	}

	[Fact]
	public async Task GetFaceRoles_AuthenticatedTenant_OnPublicFace_StillExcludesFaceAdmin()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
		var response = await _publicFace.GetAsync("/api/faces/face-roles");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var arr = await response.Content.ReadFromJsonAsync<JsonElement>();
		JsonArrayContainsRoleName(arr!, UserRole.FaceRoleNames.FaceAdmin).Should().BeFalse();
	}

	[Fact]
	public async Task PostPageTypes_Tenant_OnPublicFace_IsForbidden()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
		var response = await _publicFace.PostAsJsonAsync("/api/pagetypes", new { index = $"tenant_pt_{Guid.NewGuid():N}" });
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GetFaceRoles_Anonymous_OnPublicFace_ExcludesFaceAdmin()
	{
		var response = await _publicFace.GetAsync("/api/faces/face-roles");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var arr = await response.Content.ReadFromJsonAsync<JsonElement>();
		arr!.ValueKind.Should().Be(JsonValueKind.Array);
		JsonArrayContainsRoleName(arr, UserRole.FaceRoleNames.FaceAdmin).Should().BeFalse();
		JsonArrayContainsRoleName(arr, UserRole.FaceRoleNames.FaceUser).Should().BeTrue();
		JsonArrayContainsRoleName(arr, UserRole.FaceRoleNames.FaceHost).Should().BeTrue();
	}

	[Fact]
	public async Task GetFaceRoles_PlatformAdmin_OnAdminFace_IncludesFaceAdmin()
	{
		var token = await AclTestClients.GetPlatformAdminTokenAsync(_oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await _adminFace.GetAsync("/api/faces/face-roles");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var arr = await response.Content.ReadFromJsonAsync<JsonElement>();
		arr!.ValueKind.Should().Be(JsonValueKind.Array);
		JsonArrayContainsRoleName(arr, UserRole.FaceRoleNames.FaceAdmin).Should().BeTrue();
	}

	[Fact]
	public async Task SetMyFaceRole_Tenant_SelectingFaceAdmin_IsForbidden()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);

		var operatorToken = await AclTestClients.GetPlatformAdminTokenAsync(_oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
		var rolesResponse = await _adminFace.GetAsync("/api/faces/face-roles");
		rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var adminRoles = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
		adminRoles!.ValueKind.Should().Be(JsonValueKind.Array);
		adminRoles.GetArrayLength().Should().BeGreaterThan(0);
		var faceAdminId = JsonArrayRoleIdByName(adminRoles, UserRole.FaceRoleNames.FaceAdmin);
		faceAdminId.Should().NotBeNull();

		var cfg = await _publicFace.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		cfg.Should().NotBeNull();
		var faceId = PublicFaceIdFromConfig(cfg!);

		var put = await _publicFace.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = faceAdminId });
		put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task SetMyFaceRole_Tenant_SelectingFaceUser_IsOk()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);

		var rolesResponse = await _publicFace.GetAsync("/api/faces/face-roles");
		rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var adminRoles = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
		var faceUserId = JsonArrayRoleIdByName(adminRoles, UserRole.FaceRoleNames.FaceUser);
		faceUserId.Should().NotBeNull();

		var cfg = await _publicFace.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		cfg.Should().NotBeNull();
		var faceId = PublicFaceIdFromConfig(cfg!);

		var put = await _publicFace.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = faceUserId });
		put.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task MeCapabilities_BareApi_ReturnsBadRequest()
	{
		var token = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_unscoped.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var response = await _unscoped.GetAsync("/api/me/capabilities");
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("Face URL prefix is required");
	}

	[Fact]
	public async Task MeCapabilities_PublicFace_NewTenant_HasSessionSelfServiceAndFaceMember_FromRegistrationDefaultHost()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);

		var response = await _publicFace.GetAsync("/api/me/capabilities");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var root = await response.Content.ReadFromJsonAsync<JsonElement>();
		root!.GetProperty("globalRole").GetString().Should().Be(UserRole.GlobalRoleNames.User);
		root.GetProperty("isAdminFaceScope").GetBoolean().Should().BeFalse();
		root.GetProperty("myFaceRoleName").GetString().Should().Be(UserRole.FaceRoleNames.FaceHost);
		var perms = root.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
		perms.Should().Contain(AclPermissionKeys.TenantSession);
		perms.Should().Contain(AclPermissionKeys.FaceRoleSelfService);
		perms.Should().Contain(AclPermissionKeys.FaceMember);
		perms.Should().NotContain(AclPermissionKeys.PlatformAdmin);
		perms.Should().NotContain(AclPermissionKeys.PlatformPagetypeMutate);
	}

	[Fact]
	public async Task MeCapabilities_PublicFace_AfterFaceUserAssignment_IncludesFaceMember()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);

		var rolesResponse = await _publicFace.GetAsync("/api/faces/face-roles");
		var adminRoles = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
		var faceUserId = JsonArrayRoleIdByName(adminRoles, UserRole.FaceRoleNames.FaceUser);
		faceUserId.Should().NotBeNull();

		var cfg = await _publicFace.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		var faceId = PublicFaceIdFromConfig(cfg!);
		(await _publicFace.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = faceUserId }))
			.StatusCode.Should().Be(HttpStatusCode.OK);

		var cap = await _publicFace.GetAsync("/api/me/capabilities");
		cap.StatusCode.Should().Be(HttpStatusCode.OK);
		var root = await cap.Content.ReadFromJsonAsync<JsonElement>();
		var perms = root!.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
		perms.Should().Contain(AclPermissionKeys.FaceMember);
		root.GetProperty("myFaceRoleName").GetString().Should().Be(UserRole.FaceRoleNames.FaceUser);
	}

	[Fact]
	public async Task MeCapabilities_AdminFace_GlobalAdmin_ExcludesPlatformOperatorPermissions()
	{
		var adminToken = await AclTestClients.GetGlobalAdminTokenAsync(_oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await _adminFace.GetAsync("/api/me/capabilities");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var root = await response.Content.ReadFromJsonAsync<JsonElement>();
		root!.GetProperty("isAdminFaceScope").GetBoolean().Should().BeTrue();
		root.GetProperty("globalRole").GetString().Should().Be(UserRole.GlobalRoleNames.Admin);
		var perms = root.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
		perms.Should().NotContain(AclPermissionKeys.PlatformAdmin);
		perms.Should().NotContain(AclPermissionKeys.PlatformPagetypeMutate);
		perms.Should().NotContain(AclPermissionKeys.PlatformSuper);
		perms.Should().Contain(AclPermissionKeys.TenantSession);
		perms.Should().NotContain(AclPermissionKeys.FaceRoleSelfService);
	}

	[Fact]
	public async Task MeCapabilities_Unauthorized_WithoutBearer()
	{
		_publicFace.DefaultRequestHeaders.Authorization = null;
		var response = await _publicFace.GetAsync("/api/me/capabilities");
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task MeCapabilities_SuperAdmin_OnAdminFace_IncludesPlatformSuper()
	{
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var response = await _adminFace.GetAsync("/api/me/capabilities");
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var root = await response.Content.ReadFromJsonAsync<JsonElement>();
		root!.GetProperty("globalRole").GetString().Should().Be(UserRole.GlobalRoleNames.SuperAdmin);
		var perms = root.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
		perms.Should().Contain(AclPermissionKeys.PlatformSuper);
		perms.Should().Contain(AclPermissionKeys.PlatformAdmin);
		perms.Should().Contain(AclPermissionKeys.PlatformPagetypeMutate);
	}

	[Fact]
	public async Task PostPageTypes_PlatformAdmin_OnPublicFace_IsForbidden()
	{
		var adminToken = await AclTestClients.GetGlobalAdminTokenAsync(_oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
		var response = await _publicFace.PostAsJsonAsync("/api/pagetypes", new { index = $"admin_on_public_{Guid.NewGuid():N}" });
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GetFaces_TenantUser_OnAdminFace_IsForbidden()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
		var response = await _adminFace.GetAsync("/api/faces");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task SetMyFaceRole_BadRequest_WhenUserRoleIdMissingOrZero()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
		var cfg = await _publicFace.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		var faceId = PublicFaceIdFromConfig(cfg!);

		var r1 = await _publicFace.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = 0 });
		r1.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var r2 = await _publicFace.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { });
		r2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task SetMyFaceRole_BadRequest_WhenRoleIsGlobalNotFaceScoped()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
		var cfg = await _publicFace.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		var faceId = PublicFaceIdFromConfig(cfg!);

		int globalUserRoleId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var row = await db.UserRoles.AsNoTracking()
				.FirstAsync(r => r.Name == UserRole.GlobalRoleNames.User && r.Scope == RoleScope.Global);
			globalUserRoleId = row.Id;
		}

		var put = await _publicFace.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = globalUserRoleId });
		put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task SetMyFaceRole_NotFound_WhenTargetFaceIdNotScopedFace()
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
		var put = await _publicFace.PutAsJsonAsync("/api/faces/999997/my-role", new { userRoleId = 1 });
		put.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Theory]
	[InlineData(UserRole.FaceRoleNames.Inzerent)]
	[InlineData(UserRole.FaceRoleNames.Subscriber)]
	[InlineData(UserRole.FaceRoleNames.FaceHost)]
	public async Task SetMyFaceRole_AllowsOtherSelfServiceRoles(string roleName)
	{
		var tenantToken = await AclTestClients.RegisterAndGetTokenAsync(_factory, _oauth);
		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
		var rolesResponse = await _publicFace.GetAsync("/api/faces/face-roles");
		var arr = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
		var roleId = JsonArrayRoleIdByName(arr!, roleName);
		roleId.Should().NotBeNull("role {0} must appear in tenant face-roles list", roleName);
		var cfg = await _publicFace.GetFromJsonAsync<JsonElement[]>("/api/faces/config");
		var faceId = PublicFaceIdFromConfig(cfg!);
		var put = await _publicFace.PutAsJsonAsync($"/api/faces/{faceId}/my-role", new { userRoleId = roleId });
		put.StatusCode.Should().Be(HttpStatusCode.OK, "self-assign {0} should succeed", roleName);
	}

	[Fact]
	public async Task MeCapabilities_PermissionsAreSortedAndUnique()
	{
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		_adminFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var root = await _adminFace.GetFromJsonAsync<JsonElement>("/api/me/capabilities");
		var list = root!.GetProperty("permissions").EnumerateArray().Select(p => p!.GetString()!).ToList();
		list.Should().Equal(list.OrderBy(x => x, StringComparer.Ordinal));
		list.Should().OnlyHaveUniqueItems();
	}

	[Fact]
	public async Task PutPageTypes_PlatformAdmin_OnPublicFace_IsForbidden()
	{
		var globalAdminToken = await AclTestClients.GetGlobalAdminTokenAsync(_oauth);
		var operatorToken = await AclTestClients.GetPlatformAdminTokenAsync(_oauth);
		using var adminOnAdmin = AclTestClients.CreateAdminFaceClient(_factory);
		adminOnAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
		var createResponse = await adminOnAdmin.PostAsJsonAsync("/api/pagetypes", new { index = $"putdeny_{Guid.NewGuid():N}" });
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("id").GetInt32();

		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", globalAdminToken);
		var put = await _publicFace.PutAsJsonAsync($"/api/pagetypes/{id}", new { index = $"nope_{Guid.NewGuid():N}" });
		put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DeletePageTypes_PlatformAdmin_OnPublicFace_IsForbidden()
	{
		var globalAdminToken = await AclTestClients.GetGlobalAdminTokenAsync(_oauth);
		var operatorToken = await AclTestClients.GetPlatformAdminTokenAsync(_oauth);
		using var adminOnAdmin = AclTestClients.CreateAdminFaceClient(_factory);
		adminOnAdmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
		var createResponse = await adminOnAdmin.PostAsJsonAsync("/api/pagetypes", new { index = $"deldeny_{Guid.NewGuid():N}" });
		createResponse.EnsureSuccessStatusCode();
		var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
		var id = created.GetProperty("id").GetInt32();

		_publicFace.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", globalAdminToken);
		var del = await _publicFace.DeleteAsync($"/api/pagetypes/{id}");
		del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}
}
