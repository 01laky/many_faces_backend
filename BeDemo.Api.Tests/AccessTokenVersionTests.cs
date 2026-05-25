using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;

namespace BeDemo.Api.Tests;

/// <summary>
/// J6: access JWT carries <c>atv</c> claim; bumping <see cref="Models.ApplicationUser.AccessTokenVersion"/> invalidates outstanding access tokens.
/// </summary>
public class AccessTokenVersionTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public AccessTokenVersionTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task Authenticated_Request_Fails_After_AccessTokenVersion_Bump()
	{
		var client = _factory.CreateClient();
		var email = $"atv_{Guid.NewGuid():N}@test.com";
		var access = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(client, _factory, email, "Test1234!@##");
		access.Should().NotBeNullOrEmpty();

		var faceClient = _factory.CreateFaceClient("public");
		faceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

		var before = await faceClient.GetAsync("/api/me/capabilities");
		before.StatusCode.Should().Be(HttpStatusCode.OK);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var user = await db.Users.AsNoTracking().FirstAsync(u => u.Email == email);
			var tracked = await db.Users.FirstAsync(u => u.Id == user.Id);
			tracked.AccessTokenVersion++;
			await db.SaveChangesAsync();
		}

		var after = await faceClient.GetAsync("/api/me/capabilities");
		after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task Authenticated_Request_Fails_After_Global_UserRoleId_Change()
	{
		var client = _factory.CreateClient();
		var email = $"atv_role_{Guid.NewGuid():N}@test.com";
		var access = await IntegrationTestRegistration.RegisterAndGetAccessTokenViaPasswordGrantAsync(client, _factory, email, "Test1234!@##");

		var faceClient = _factory.CreateFaceClient("public");
		faceClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
		(await faceClient.GetAsync("/api/me/capabilities")).StatusCode.Should().Be(HttpStatusCode.OK);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var userRole = await db.UserRoles.AsNoTracking().FirstAsync(r => r.Name == UserRole.GlobalRoleNames.Admin);
			var user = await db.Users.FirstAsync(u => u.Email == email);
			user.UserRoleId = userRole.Id;
			await db.SaveChangesAsync();
		}

		var after = await faceClient.GetAsync("/api/me/capabilities");
		after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
