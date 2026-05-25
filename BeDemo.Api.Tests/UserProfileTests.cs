/*
 * UserProfileTests.cs - Unit tests for UserProfile functionality
 *
 * Tests that UserProfile is automatically created on user registration
 * and that one-to-one relationship works correctly.
 */

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BeDemo.Api.Models.DTOs;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Tests;

/// <summary>
/// Unit tests for UserProfile entity and registration flow
/// </summary>
public class UserProfileTests : IClassFixture<CustomWebApplicationFactory<Program>>, IDisposable
{
	private readonly CustomWebApplicationFactory<Program> _factory;
	private readonly HttpClient _client;

	public UserProfileTests(CustomWebApplicationFactory<Program> factory)
	{
		_factory = factory;
		_client = _factory.CreateClient();
	}

	[Fact]
	public async Task Register_ShouldCreateUserProfile_WhenUserIsRegistered()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		const string password = "Test1234!@##";

		var completed = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email,
			password);

		completed.UserId.Should().NotBeNullOrEmpty();

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var userProfile = await context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == completed.UserId);

		userProfile.Should().NotBeNull();
		userProfile!.UserId.Should().Be(completed.UserId);
		userProfile.Id.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task UserProfile_ShouldHaveOneToOneRelationship_WithApplicationUser()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		const string password = "Test1234!@##";

		var completed = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email,
			password,
			"Test",
			"User");
		var userId = completed.UserId;

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var user = await context.Users
			.Include(u => u.UserProfile)
			.FirstOrDefaultAsync(u => u.Id == userId);

		user.Should().NotBeNull();
		user!.UserProfile.Should().NotBeNull();
		user.UserProfile!.UserId.Should().Be(userId);

		var profile = await context.UserProfiles
			.Include(up => up.User)
			.FirstOrDefaultAsync(up => up.UserId == userId);

		profile.Should().NotBeNull();
		profile!.User.Should().NotBeNull();
		profile.User.Id.Should().Be(userId);
	}

	[Fact]
	public async Task UserProfile_ShouldHaveAutoIncrementId()
	{
		const string password = "Test1234!@##";
		var email1 = $"test_{Guid.NewGuid()}@test.com";
		var email2 = $"test_{Guid.NewGuid()}@test.com";

		var completed1 = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email1,
			password,
			"User",
			"One");
		var completed2 = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email2,
			password,
			"User",
			"Two");

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var profileId1 = (await context.UserProfiles.FirstAsync(up => up.UserId == completed1.UserId)).Id;
		var profileId2 = (await context.UserProfiles.FirstAsync(up => up.UserId == completed2.UserId)).Id;

		profileId1.Should().NotBe(profileId2);
		profileId2.Should().BeGreaterThan(profileId1);
	}

	[Fact]
	public async Task UserProfile_ShouldBeCreatedWithDefaultValues()
	{
		var email = $"test_{Guid.NewGuid()}@test.com";
		const string password = "Test1234!@##";

		var completed = await IntegrationTestRegistration.CompleteRegistrationAsync(
			_client,
			_factory,
			email,
			password,
			"Test",
			"User");
		var userId = completed.UserId;

		using var scope = _factory.Services.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var userProfile = await context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);

		userProfile.Should().NotBeNull();
		userProfile!.Nickname.Should().BeNull();
		userProfile.AvatarUrl.Should().BeNull();
	}

	public void Dispose() => _client.Dispose();
}
