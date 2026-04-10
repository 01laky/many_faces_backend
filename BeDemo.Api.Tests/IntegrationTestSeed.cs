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
    public const string Password = "Test123!@#";

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
}
