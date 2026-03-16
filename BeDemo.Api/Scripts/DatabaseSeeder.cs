using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Scripts;

/// <summary>
/// Database seeder - seeds initial data for PageTypes, Faces, Pages, and Users
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
        catch (Exception)
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
        var globalRoles = new[]
        {
            new { Name = UserRole.GlobalRoleNames.SuperAdmin, Description = "Super Administrator - Full system access" },
            new { Name = UserRole.GlobalRoleNames.Admin, Description = "Administrator - Administrative access" },
            new { Name = UserRole.GlobalRoleNames.User, Description = "User - Standard user access" },
            new { Name = UserRole.GlobalRoleNames.Host, Description = "Host - Global host role" },
        };

        var faceRoles = new[]
        {
            new { Name = UserRole.FaceRoleNames.FaceAdmin, Description = "Face Administrator - Manages faces and pages" },
            new { Name = UserRole.FaceRoleNames.FaceUser, Description = "Face User - User with face-specific access" },
            new { Name = UserRole.FaceRoleNames.Inzerent, Description = "Inzerent - Advertisement manager" },
            new { Name = UserRole.FaceRoleNames.Subscriber, Description = "Subscriber - Premium user access" },
            new { Name = UserRole.FaceRoleNames.FaceHost, Description = "Face Host - Default role per face" },
        };

        foreach (var roleData in globalRoles)
        {
            var existing = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == roleData.Name);
            if (existing == null)
            {
                context.UserRoles.Add(new UserRole
                {
                    Name = roleData.Name,
                    Description = roleData.Description,
                    Scope = RoleScope.Global,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (existing.Scope != RoleScope.Global)
            {
                existing.Scope = RoleScope.Global;
                existing.Description = roleData.Description;
            }
        }

        foreach (var roleData in faceRoles)
        {
            var existing = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == roleData.Name);
            if (existing == null)
            {
                context.UserRoles.Add(new UserRole
                {
                    Name = roleData.Name,
                    Description = roleData.Description,
                    Scope = RoleScope.Face,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (existing.Scope != RoleScope.Face)
            {
                existing.Scope = RoleScope.Face;
                existing.Description = roleData.Description;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPageTypesAsync(ApplicationDbContext context)
    {
        var pageTypeIndices = new[] { "home", "list", "detail", "edit", "create", "static", "wall" };

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
        var wallPageType = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "wall");

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
                IsPublic = true,
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
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
            };
            context.Faces.Add(basicFace);
            await context.SaveChangesAsync();

            // Pages for basic face (non-public: include Wall)
            var basicPages = new List<Page>
            {
                new Page { FaceId = basicFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = basicFace.Id, PageTypeId = listPageType.Id, Name = "List", Path = "/list", Index = 1, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = basicFace.Id, PageTypeId = detailPageType.Id, Name = "Detail", Path = "/detail", Index = 2, CreatedAt = DateTime.UtcNow },
            };
            if (wallPageType != null)
            {
                basicPages.Add(new Page { FaceId = basicFace.Id, PageTypeId = wallPageType.Id, Name = "Wall", Path = "/wall", Index = 3, CreatedAt = DateTime.UtcNow });
            }
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
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
            };
            context.Faces.Add(konceptFace);
            await context.SaveChangesAsync();

            // Pages for koncept face (non-public: include Wall)
            var konceptPages = new List<Page>
            {
                new Page { FaceId = konceptFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = konceptFace.Id, PageTypeId = listPageType.Id, Name = "List", Path = "/list", Index = 1, CreatedAt = DateTime.UtcNow },
                new Page { FaceId = konceptFace.Id, PageTypeId = detailPageType.Id, Name = "Detail", Path = "/detail", Index = 2, CreatedAt = DateTime.UtcNow },
            };
            if (wallPageType != null)
            {
                konceptPages.Add(new Page { FaceId = konceptFace.Id, PageTypeId = wallPageType.Id, Name = "Wall", Path = "/wall", Index = 3, CreatedAt = DateTime.UtcNow });
            }
            context.Pages.AddRange(konceptPages);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds 2 admin users and 30 regular users with UserProfile and UserFaceProfile for each face
    /// </summary>
    public static async Task SeedUsersAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        // Get global roles (for ApplicationUser) and face role FACE_HOST (default per face)
        var adminRole = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.Admin);
        var userRole = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.User);
        var faceHostRole = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost);

        if (adminRole == null || userRole == null)
        {
            Console.WriteLine("⚠️  Global roles not found. Skipping user seeding.");
            return;
        }

        // Get all faces for creating UserFaceProfiles
        var faces = await context.Faces.ToListAsync();

        // Temporarily remove password validators for simple seed passwords
        var validators = userManager.PasswordValidators.ToList();
        userManager.PasswordValidators.Clear();

        try
        {
            // --- 2 Admin users ---
            var adminUsers = new[]
            {
                new { Email = "admin1@demo.com", FirstName = "Adam", LastName = "Novák", Nickname = "adam.novak" },
                new { Email = "admin2@demo.com", FirstName = "Eva", LastName = "Kováčová", Nickname = "eva.kovacova" },
            };

            foreach (var adminData in adminUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(adminData.Email);
                if (existingUser != null) continue;

                var user = new ApplicationUser
                {
                    UserName = adminData.Email,
                    Email = adminData.Email,
                    EmailConfirmed = true,
                    FirstName = adminData.FirstName,
                    LastName = adminData.LastName,
                    CreatedAt = DateTime.UtcNow,
                    UserRoleId = adminRole.Id
                };

                var result = await userManager.CreateAsync(user, "admin");
                if (!result.Succeeded)
                {
                    Console.WriteLine($"❌ Failed to create admin {adminData.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    continue;
                }

                await context.SaveChangesAsync();

                // Create UserProfile
                var profile = new UserProfile
                {
                    UserId = user.Id,
                    Nickname = adminData.Nickname,
                    Age = 35,
                    Rod = "M",
                    CreatedAt = DateTime.UtcNow
                };
                context.UserProfiles.Add(profile);
                await context.SaveChangesAsync();

                // Create UserFaceProfile and UserFaceRole (FACE_HOST) for each face
                foreach (var face in faces)
                {
                    context.UserFaceProfiles.Add(new UserFaceProfile
                    {
                        UserProfileId = profile.Id,
                        FaceId = face.Id,
                        DisplayName = $"{adminData.FirstName} {adminData.LastName}",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    if (faceHostRole != null)
                    {
                        context.UserFaceRoles.Add(new UserFaceRole
                        {
                            UserId = user.Id,
                            FaceId = face.Id,
                            UserRoleId = faceHostRole.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                await context.SaveChangesAsync();

                Console.WriteLine($"✅ Admin seeded: {adminData.Email}");
            }

            // --- 30 Regular users ---
            var firstNames = new[] { "Ján", "Peter", "Martin", "Tomáš", "Lukáš", "Marek", "Michal", "Ondrej", "Dávid", "Jakub",
                                     "Mária", "Anna", "Katarína", "Jana", "Zuzana", "Monika", "Lucia", "Petra", "Simona", "Lenka",
                                     "Róbert", "Štefan", "Pavol", "Daniel", "Matúš", "Filip", "Andrej", "Samuel", "Richard", "Patrik" };
            var lastNames = new[] { "Horváth", "Kováč", "Varga", "Tóth", "Nagy", "Baláž", "Molnár", "Szabó", "Novák", "Fekete",
                                    "Bílik", "Krajčír", "Kučera", "Polák", "Valent", "Hudák", "Šimko", "Jurčo", "Hruška", "Majer",
                                    "Lacko", "Gajdoš", "Rusnák", "Sedlák", "Vrábel", "Haluška", "Mišík", "Bartoš", "Čierny", "Zelený" };

            for (int i = 0; i < 30; i++)
            {
                var email = $"user{i + 1:D2}@demo.com";
                var existingUser = await userManager.FindByEmailAsync(email);
                if (existingUser != null) continue;

                var firstName = firstNames[i];
                var lastName = lastNames[i];

                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    CreatedAt = DateTime.UtcNow,
                    UserRoleId = userRole.Id
                };

                var result = await userManager.CreateAsync(user, "user123");
                if (!result.Succeeded)
                {
                    Console.WriteLine($"❌ Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    continue;
                }

                await context.SaveChangesAsync();

                // Create UserProfile
                var profile = new UserProfile
                {
                    UserId = user.Id,
                    Nickname = $"{firstName.ToLower()}.{lastName.ToLower()}",
                    Age = 20 + (i % 30),
                    Rod = i < 10 ? "M" : (i < 20 ? "F" : "M"),
                    CreatedAt = DateTime.UtcNow
                };
                context.UserProfiles.Add(profile);
                await context.SaveChangesAsync();

                // Create UserFaceProfile and UserFaceRole (FACE_HOST) for each face
                foreach (var face in faces)
                {
                    context.UserFaceProfiles.Add(new UserFaceProfile
                    {
                        UserProfileId = profile.Id,
                        FaceId = face.Id,
                        DisplayName = $"{firstName} {lastName}",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    if (faceHostRole != null)
                    {
                        context.UserFaceRoles.Add(new UserFaceRole
                        {
                            UserId = user.Id,
                            FaceId = face.Id,
                            UserRoleId = faceHostRole.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                await context.SaveChangesAsync();
            }

            Console.WriteLine($"✅ Seeded 2 admins and 30 users successfully");
        }
        finally
        {
            // Restore password validators
            foreach (var validator in validators)
            {
                userManager.PasswordValidators.Add(validator);
            }
        }
    }
}
