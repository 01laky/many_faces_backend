using System.Net.Http.Json;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Models.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BeDemo.Api.Tests;

/// <summary>
/// Ensures a predictable global Admin user exists for tests that need <c>/admin/...</c> face scope (face CRUD, etc.).
/// </summary>
public static class IntegrationTestSeed
{
	public const string Email = "integration-admin@test.com";
	public const string SuperAdminEmail = "integration-superadmin@test.com";
	public const string Password = IntegrationTestCredentials.DefaultPassword;

	public static async Task EnsureAsync(IServiceProvider services, CancellationToken cancellationToken = default)
	{
		using var scope = services.CreateScope();
		var sp = scope.ServiceProvider;
		var context = sp.GetRequiredService<ApplicationDbContext>();
		var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

		var adminRole = await context.UserRoles.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.Admin, cancellationToken);
		if (adminRole == null)
			throw new InvalidOperationException("Global Admin role missing; run DatabaseSeeder first.");

		if (await userManager.FindByEmailAsync(Email) != null)
			return;

		var user = new ApplicationUser
		{
			UserName = Email,
			Email = Email,
			EmailConfirmed = true,
			FirstName = "Integration",
			LastName = "Admin",
			UserRoleId = adminRole.Id,
		};

		var result = await userManager.CreateAsync(user, Password);
		if (!result.Succeeded)
		{
			throw new InvalidOperationException(
				"Failed to seed integration admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
		}

		var profile = new UserProfile
		{
			UserId = user.Id,
			Nickname = "integration.admin",
			CreatedAt = DateTime.UtcNow,
		};
		context.UserProfiles.Add(profile);
		await context.SaveChangesAsync(cancellationToken);

		var faces = await context.Faces.AsNoTracking().Select(f => f.Id).ToListAsync(cancellationToken);
		var hostRole = await context.UserRoles.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost, cancellationToken);
		if (hostRole == null)
			throw new InvalidOperationException("FaceHost role missing.");

		foreach (var faceId in faces)
		{
			context.UserFaceProfiles.Add(new UserFaceProfile
			{
				UserProfileId = profile.Id,
				FaceId = faceId,
				Visited = false,
				FaceRoleIntroCompleted = false,
				CreatedAt = DateTime.UtcNow,
			});

			context.UserFaceRoles.Add(new UserFaceRole
			{
				UserId = user.Id,
				FaceId = faceId,
				UserRoleId = hostRole.Id,
				CreatedAt = DateTime.UtcNow,
			});
		}

		await context.SaveChangesAsync(cancellationToken);
	}

	/// <summary>Global SUPER_ADMIN for capabilities / platform:super tests (A7).</summary>
	public static async Task EnsureSuperAdminAsync(IServiceProvider services, CancellationToken cancellationToken = default)
	{
		using var scope = services.CreateScope();
		var sp = scope.ServiceProvider;
		var context = sp.GetRequiredService<ApplicationDbContext>();
		var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

		var superRole = await context.UserRoles.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin, cancellationToken);
		if (superRole == null)
			throw new InvalidOperationException("SUPER_ADMIN role missing.");

		if (await userManager.FindByEmailAsync(SuperAdminEmail) != null)
			return;

		var user = new ApplicationUser
		{
			UserName = SuperAdminEmail,
			Email = SuperAdminEmail,
			EmailConfirmed = true,
			FirstName = "Integration",
			LastName = "SuperAdmin",
			UserRoleId = superRole.Id,
		};

		var result = await userManager.CreateAsync(user, Password);
		if (!result.Succeeded)
		{
			throw new InvalidOperationException(
				"Failed to seed integration super-admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
		}

		var profile = new UserProfile
		{
			UserId = user.Id,
			Nickname = "integration.superadmin",
			CreatedAt = DateTime.UtcNow,
		};
		context.UserProfiles.Add(profile);
		await context.SaveChangesAsync(cancellationToken);

		var faces = await context.Faces.AsNoTracking().Select(f => f.Id).ToListAsync(cancellationToken);
		var hostRole = await context.UserRoles.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost, cancellationToken);
		if (hostRole == null)
			throw new InvalidOperationException("FaceHost role missing.");

		foreach (var faceId in faces)
		{
			context.UserFaceProfiles.Add(new UserFaceProfile
			{
				UserProfileId = profile.Id,
				FaceId = faceId,
				Visited = false,
				FaceRoleIntroCompleted = false,
				CreatedAt = DateTime.UtcNow,
			});

			context.UserFaceRoles.Add(new UserFaceRole
			{
				UserId = user.Id,
				FaceId = faceId,
				UserRoleId = hostRole.Id,
				CreatedAt = DateTime.UtcNow,
			});
		}

		await context.SaveChangesAsync(cancellationToken);
	}

	/// <summary>
	/// Legacy integration tests assume operator AI features are available.
	/// Sets the singleton row to enabled without running Activate AI health/warm orchestration.
	/// </summary>
	public static async Task EnsureOperatorAiEnabledForIntegrationTestsAsync(
		IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		using var scope = services.CreateScope();
		var sp = scope.ServiceProvider;
		var context = sp.GetRequiredService<ApplicationDbContext>();
		var row = await context.OperatorAiSystemSettings.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);
		var now = DateTime.UtcNow;
		if (row == null)
		{
			context.OperatorAiSystemSettings.Add(new OperatorAiSystemSettings
			{
				Id = 1,
				AiEnabled = true,
				UpdatedAtUtc = now,
				LastEnabledAtUtc = now,
				LastEnableHealthStatus = "test_harness",
			});
		}
		else
		{
			row.AiEnabled = true;
			row.UpdatedAtUtc = now;
			row.LastEnabledAtUtc ??= now;
			row.LastEnableHealthStatus ??= "test_harness";
		}

		await context.SaveChangesAsync(cancellationToken);

		var provider = sp.GetRequiredService<BeDemo.Api.Services.OperatorAi.IOperatorAiSystemSettingsProvider>();
		if (provider is BeDemo.Api.Services.OperatorAi.OperatorAiSystemSettingsService settingsService)
			settingsService.InvalidateCache();
	}

	public static async Task SetOperatorAiEnabledAsync(
		IServiceProvider services,
		bool enabled,
		CancellationToken cancellationToken = default)
	{
		using var scope = services.CreateScope();
		var sp = scope.ServiceProvider;
		var context = sp.GetRequiredService<ApplicationDbContext>();
		var row = await context.OperatorAiSystemSettings.SingleOrDefaultAsync(e => e.Id == 1, cancellationToken);
		var now = DateTime.UtcNow;
		if (row == null)
		{
			context.OperatorAiSystemSettings.Add(new OperatorAiSystemSettings
			{
				Id = 1,
				AiEnabled = enabled,
				UpdatedAtUtc = now,
				LastEnabledAtUtc = enabled ? now : null,
				LastEnableHealthStatus = enabled ? "test" : null,
			});
		}
		else
		{
			row.AiEnabled = enabled;
			row.UpdatedAtUtc = now;
			if (enabled)
				row.LastEnabledAtUtc = now;
		}

		await context.SaveChangesAsync(cancellationToken);

		var provider = sp.GetRequiredService<BeDemo.Api.Services.OperatorAi.IOperatorAiSystemSettingsProvider>();
		if (provider is BeDemo.Api.Services.OperatorAi.OperatorAiSystemSettingsService settingsService)
			settingsService.InvalidateCache();
	}

	public static async Task<string> GetAdminAccessTokenAsync(HttpClient oauthClient, CancellationToken cancellationToken = default)
	{
		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = Email,
			Password = Password,
		};

		using var response = await oauthClient.PostAsJsonAsync("/api/oauth2/token", tokenRequest, cancellationToken);
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>(cancellationToken: cancellationToken);
		if (string.IsNullOrEmpty(body?.AccessToken))
			throw new InvalidOperationException("Token response missing access_token.");
		return body.AccessToken;
	}

	public static async Task<string> GetSuperAdminAccessTokenAsync(HttpClient oauthClient, CancellationToken cancellationToken = default)
	{
		var tokenRequest = new OAuth2TokenRequest
		{
			GrantType = "password",
			ClientId = "be-demo-client",
			ClientSecret = "be-demo-secret-very-strong-key",
			Username = SuperAdminEmail,
			Password = Password,
		};

		using var response = await oauthClient.PostAsJsonAsync("/api/oauth2/token", tokenRequest, cancellationToken);
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>(cancellationToken: cancellationToken);
		if (string.IsNullOrEmpty(body?.AccessToken))
			throw new InvalidOperationException("Token response missing access_token.");
		return body.AccessToken;
	}
}
