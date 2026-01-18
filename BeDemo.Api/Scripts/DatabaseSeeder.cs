using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Scripts;

/// <summary>
/// Database seeder - seeds initial data for PageTypes, Faces, and Pages
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Apply migrations - this will create database and tables if they don't exist
        // MigrateAsync is safer than EnsureCreatedAsync when using migrations
        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            // If migrations fail and database doesn't exist, ensure it's created
            // This is a fallback for first-time setup
            if (!await context.Database.CanConnectAsync())
            {
                await context.Database.EnsureCreatedAsync();
            }
            else
            {
                // Re-throw if database exists but migrations failed
                throw;
            }
        }

        // Seed UserRoles (must be before PageTypes to ensure roles exist for users)
        await SeedUserRolesAsync(context);

        // Seed PageTypes
        await SeedPageTypesAsync(context);

        // Seed Faces and Pages
        await SeedFacesAndPagesAsync(context);

        await context.SaveChangesAsync();
    }

    public static async Task SeedUserRolesAsync(ApplicationDbContext context)
    {
        var roles = new[]
        {
            new { Name = UserRole.RoleNames.SuperAdmin, Description = "Super Administrator - Full system access" },
            new { Name = UserRole.RoleNames.Admin, Description = "Administrator - Administrative access" },
            new { Name = UserRole.RoleNames.FaceAdmin, Description = "Face Administrator - Manages faces and pages" },
            new { Name = UserRole.RoleNames.Inzerent, Description = "Inzerent - Advertisement manager" },
            new { Name = UserRole.RoleNames.Subscriber, Description = "Subscriber - Premium user access" },
            new { Name = UserRole.RoleNames.User, Description = "User - Standard user access" }
        };

        foreach (var roleData in roles)
        {
            var existingRole = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == roleData.Name);
            if (existingRole == null)
            {
                context.UserRoles.Add(new UserRole
                {
                    Name = roleData.Name,
                    Description = roleData.Description,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPageTypesAsync(ApplicationDbContext context)
    {
        var pageTypeIndices = new[] { "home", "list", "detail", "edit", "create", "static" };

        foreach (var index in pageTypeIndices)
        {
            var existingPageType = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == index);
            if (existingPageType == null)
            {
                context.PageTypes.Add(new PageType
                {
                    Index = index,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedFacesAndPagesAsync(ApplicationDbContext context)
    {
        // Get PageTypes
        var homePageType = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "home");
        var staticPageType = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "static");
        var listPageType = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "list");
        var detailPageType = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "detail");

        if (homePageType == null || staticPageType == null || listPageType == null || detailPageType == null)
        {
            throw new InvalidOperationException("Required PageTypes not found. Please seed PageTypes first.");
        }

        // Face 1: public
        var publicFace = await context.Faces.FirstOrDefaultAsync(f => f.Index == "public");
        if (publicFace == null)
        {
            publicFace = new Face
            {
                Index = "public",
                Title = "Public",
                Description = "Public face",
                Color = "#007bff",
                CreatedAt = DateTime.UtcNow,
            };
            context.Faces.Add(publicFace);
            await context.SaveChangesAsync();

            // Pages for public face
            var publicPages = new[]
            {
                new Page { FaceId = publicFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = publicFace.Id, PageTypeId = staticPageType.Id, Name = "Register", Path = "/register", Index = 1, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = publicFace.Id, PageTypeId = staticPageType.Id, Name = "Login", Path = "/login", Index = 2, CreatedAt = DateTime.UtcNow },
            };

            context.Pages.AddRange(publicPages);
        }

        // Face 2: basic
        var basicFace = await context.Faces.FirstOrDefaultAsync(f => f.Index == "basic");
        if (basicFace == null)
        {
            basicFace = new Face
            {
                Index = "basic",
                Title = "Basic",
                Description = "Basic face",
                Color = "#28a745",
                CreatedAt = DateTime.UtcNow,
            };
            context.Faces.Add(basicFace);
            await context.SaveChangesAsync();

            // Pages for basic face
            var basicPages = new[]
            {
                new Page { FaceId = basicFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = basicFace.Id, PageTypeId = listPageType.Id, Name = "List", Path = "/list", Index = 1, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = basicFace.Id, PageTypeId = detailPageType.Id, Name = "Detail", Path = "/detail", Index = 2, CreatedAt = DateTime.UtcNow },
            };

            context.Pages.AddRange(basicPages);
        }

        // Face 3: koncept
        var konceptFace = await context.Faces.FirstOrDefaultAsync(f => f.Index == "koncept");
        if (konceptFace == null)
        {
            konceptFace = new Face
            {
                Index = "koncept",
                Title = "Koncept",
                Description = "Koncept face",
                Color = "#ffc107",
                CreatedAt = DateTime.UtcNow,
            };
            context.Faces.Add(konceptFace);
            await context.SaveChangesAsync();

            // Pages for koncept face
            var konceptPages = new[]
            {
                new Page { FaceId = konceptFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = konceptFace.Id, PageTypeId = listPageType.Id, Name = "List", Path = "/list", Index = 1, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = konceptFace.Id, PageTypeId = detailPageType.Id, Name = "Detail", Path = "/detail", Index = 2, CreatedAt = DateTime.UtcNow },
            };

            context.Pages.AddRange(konceptPages);
        }

        await context.SaveChangesAsync();
    }
}
