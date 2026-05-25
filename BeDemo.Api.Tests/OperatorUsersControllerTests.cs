using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Tests;

public class OperatorUsersControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _oauth;
	private readonly HttpClient _adminFace;

	public OperatorUsersControllerTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_oauth = AclTestClients.CreateOAuthClient(factory);
		_adminFace = AclTestClients.CreateAdminFaceClient(factory);
	}

	[Fact]
	public async Task GetDetail_Should403_ForPlatformAdmin_NotSuperAdmin()
	{
		var adminToken = await AclTestClients.GetGlobalAdminTokenAsync(_oauth);
		var targetId = await GetIntegrationAdminUserIdAsync();
		var req = new HttpRequestMessage(HttpMethod.Get, $"/api/operator-users/users/{targetId}/detail");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
		var res = await _adminFace.SendAsync(req);
		res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GetDetail_Should200_ForSuperAdmin()
	{
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var targetId = await GetIntegrationAdminUserIdAsync();
		var req = new HttpRequestMessage(HttpMethod.Get, $"/api/operator-users/users/{targetId}/detail");
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var res = await _adminFace.SendAsync(req);
		res.StatusCode.Should().Be(HttpStatusCode.OK);
		var json = await res.Content.ReadFromJsonAsync<JsonElement>();
		json.GetProperty("email").GetString().Should().Be(IntegrationTestSeed.Email);
	}

	[Fact]
	public async Task GlobalBan_Should403_WhenTargetIsSuperAdmin()
	{
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var superId = await GetUserIdByEmailAsync(IntegrationTestSeed.SuperAdminEmail);
		var req = new HttpRequestMessage(HttpMethod.Post, $"/api/operator-users/users/{superId}/global-ban")
		{
			Content = JsonContent.Create(new { reason = "policy violation test" }),
		};
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var res = await _adminFace.SendAsync(req);
		res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GlobalBan_AndUnban_ShouldNotTouchUserBlocks()
	{
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var targetId = await GetIntegrationAdminUserIdAsync();
		var blocksBefore = await CountUserBlocksForUserAsync(targetId);

		var banReq = new HttpRequestMessage(HttpMethod.Post, $"/api/operator-users/users/{targetId}/global-ban")
		{
			Content = JsonContent.Create(new { reason = "integration test global ban" }),
		};
		banReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		(await _adminFace.SendAsync(banReq)).StatusCode.Should().Be(HttpStatusCode.OK);

		var unbanReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/operator-users/users/{targetId}/global-ban");
		unbanReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var unbanRes = await _adminFace.SendAsync(unbanReq);
		unbanRes.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

		var blocksAfter = await CountUserBlocksForUserAsync(targetId);
		blocksAfter.Should().Be(blocksBefore);
	}

	[Fact]
	public async Task PlatformMessage_ShouldCreateMessageRow()
	{
		var token = await AclTestClients.GetPlatformSuperAdminTokenAsync(_oauth);
		var targetId = await GetIntegrationAdminUserIdAsync();
		var req = new HttpRequestMessage(HttpMethod.Post, $"/api/operator-users/users/{targetId}/platform-messages")
		{
			Content = JsonContent.Create(new { content = "Hello from platform administrator test" }),
		};
		req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		var res = await _adminFace.SendAsync(req);
		res.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var exists = await db.Messages.AnyAsync(m =>
			m.ReceiverId == targetId && m.Content.Contains("platform administrator test"));
		exists.Should().BeTrue();
	}

	private async Task<string> GetIntegrationAdminUserIdAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == IntegrationTestSeed.Email);
		return user.Id;
	}

	private async Task<string> GetUserIdByEmailAsync(string email)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == email);
		return user.Id;
	}

	private async Task<int> CountUserBlocksForUserAsync(string userId)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		return await db.UserBlocks.CountAsync(b => b.BlockerId == userId || b.BlockedId == userId);
	}
}
