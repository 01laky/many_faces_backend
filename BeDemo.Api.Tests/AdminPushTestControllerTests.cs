using System.Net;
using System.Net.Http.Headers;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Integration coverage for <see cref="Controllers.AdminPushTestController"/>.
/// </summary>
public sealed class AdminPushTestControllerTests
	: IClassFixture<CustomWebApplicationFactory<Program>>,
		IClassFixture<PushDisabledWebApplicationFactory>,
		IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly PushDisabledWebApplicationFactory _pushDisabledFactory;

	public AdminPushTestControllerTests(
		CustomWebApplicationFactory<Program> factory,
		PushDisabledWebApplicationFactory pushDisabledFactory)
	{
		_factory = factory;
		_pushDisabledFactory = pushDisabledFactory;
	}

	public void Dispose() { }

	[Fact]
	public async Task TestSelf_ShouldReturnUnauthorized_OnAdminFace_WithoutJwt()
	{
		var client = _factory.CreateFaceClient("admin");
		var response = await client.PostAsync("/api/admin/push/test-self", null);
		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task TestSelf_ShouldReturnForbidden_ForGlobalAdmin()
	{
		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.PostAsync("/api/admin/push/test-self", null);
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task TestSelf_ShouldReturnBadRequest_WhenPushDisabled_ForSuperAdmin()
	{
		var client = _pushDisabledFactory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
		await EnsureSuperAdminHasPushDeviceAsync(_pushDisabledFactory);

		var response = await client.PostAsync("/api/admin/push/test-self", null);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Contain("Push", "error should mention push worker configuration");
	}

	[Fact]
	public async Task TestSelf_ShouldReturnBadRequest_WhenNoDevices_ForSuperAdmin()
	{
		await EnsureSuperAdminHasNoPushDevicesAsync(_factory);

		var client = _factory.CreateFaceClient("admin");
		var token = await IntegrationTestSeed.GetSuperAdminAccessTokenAsync(client);
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		var response = await client.PostAsync("/api/admin/push/test-self", null);
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		var body = await response.Content.ReadAsStringAsync();
		body.Should().Match("*device*", because: "seeded super-admin has no push devices");
	}

	private static async Task EnsureSuperAdminHasNoPushDevicesAsync(CustomWebApplicationFactory<Program> factory)
	{
		using var scope = factory.Services.CreateScope();
		var sp = scope.ServiceProvider;
		var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
		var db = sp.GetRequiredService<ApplicationDbContext>();
		var user = await userManager.FindByEmailAsync(IntegrationTestSeed.SuperAdminEmail);
		if (user == null)
			throw new InvalidOperationException("Super-admin seed missing.");

		var rows = db.UserPushDevices.Where(d => d.UserId == user.Id).ToList();
		if (rows.Count == 0)
			return;

		db.UserPushDevices.RemoveRange(rows);
		await db.SaveChangesAsync();
	}

	private static async Task EnsureSuperAdminHasPushDeviceAsync(PushDisabledWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var sp = scope.ServiceProvider;
		var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
		var db = sp.GetRequiredService<ApplicationDbContext>();
		var user = await userManager.FindByEmailAsync(IntegrationTestSeed.SuperAdminEmail);
		if (user == null)
			throw new InvalidOperationException("Super-admin seed missing.");

		var exists = db.UserPushDevices.Any(d => d.UserId == user.Id);
		if (exists)
			return;

		var now = DateTime.UtcNow;
		db.UserPushDevices.Add(new UserPushDevice
		{
			UserId = user.Id,
			Platform = "android",
			RegistrationToken = "integration-test-push-token",
			CreatedAtUtc = now,
			UpdatedAtUtc = now,
		});
		await db.SaveChangesAsync();
	}
}
