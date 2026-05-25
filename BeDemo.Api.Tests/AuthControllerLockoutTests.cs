using System.Net;
using System.Net.Http.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>BSH3-A2: legacy cookie login lockout after repeated failures.</summary>
[Trait("Category", "BackendSecurity")]
public sealed class AuthControllerLockoutTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
	private readonly CustomWebApplicationFactory<Program> _factory;

	public AuthControllerLockoutTests(CustomWebApplicationFactory<Program> factory) => _factory = factory;

	[Fact]
	public async Task Login_locks_out_after_max_failed_attempts()
	{
		const string email = "lockout-test@demo.com";
		const string password = "LockoutTest1234!@##";

		using var scope = _factory.Services.CreateScope();
		var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
		var userRole = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
			.UserRoles.First(r => r.Name == UserRole.GlobalRoleNames.User);

		var user = new ApplicationUser
		{
			UserName = email,
			Email = email,
			EmailConfirmed = true,
			UserRoleId = userRole.Id,
		};
		(await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();

		using var client = _factory.CreateUnscopedClient();
		var maxAttempts = userManager.Options.Lockout.MaxFailedAccessAttempts;

		for (var i = 0; i < maxAttempts; i++)
		{
			using var bad = await client.PostAsJsonAsync("/api/auth/login", new
			{
				email,
				password = "wrong-password",
				rememberMe = false,
			});
			bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
		}

		using var goodAfterLockout = await client.PostAsJsonAsync("/api/auth/login", new
		{
			email,
			password,
			rememberMe = false,
		});
		goodAfterLockout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}
}
