using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Utils;
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

        // Seed ComponentTypes
        await SeedComponentTypesAsync(context);

        // Seed DisplayModes
        await SeedDisplayModesAsync(context);

        // Seed Faces and Pages
        await SeedFacesAndPagesAsync(context);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds only data (UserRoles, PageTypes, Faces, Pages) without running migrations.
    /// Use when the database already exists (e.g. in-memory or EnsureCreated).
    /// </summary>
    public static async Task SeedDataOnlyAsync(ApplicationDbContext context)
    {
        await SeedUserRolesAsync(context);
        await SeedPageTypesAsync(context);
        await SeedComponentTypesAsync(context);
        await SeedDisplayModesAsync(context);
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
        // Only CMS-facing types: home, static (login/register-style), wall. List/detail/edit/create are fixed FE routes.
        var pageTypeIndices = new[] { "home", "static", "wall" };

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

    private static async Task SeedComponentTypesAsync(ApplicationDbContext context)
    {
        var componentTypes = new[]
        {
            new { Id = (int)ComponentTypeId.Ad, Index = ComponentTypeIndex.Ad, Name = "Ad" },
            new { Id = (int)ComponentTypeId.Album, Index = ComponentTypeIndex.Album, Name = "Album" },
            new { Id = (int)ComponentTypeId.Blog, Index = ComponentTypeIndex.Blog, Name = "Blog" },
            new { Id = (int)ComponentTypeId.ChatRoom, Index = ComponentTypeIndex.ChatRoom, Name = "Chat Room" },
            new { Id = (int)ComponentTypeId.UserProfile, Index = ComponentTypeIndex.UserProfile, Name = "User Profile" },
            new { Id = (int)ComponentTypeId.Story, Index = ComponentTypeIndex.Story, Name = "Story" },
            new { Id = (int)ComponentTypeId.Reel, Index = ComponentTypeIndex.Reel, Name = "Reel" },
        };

        foreach (var ct in componentTypes)
        {
            var existing = await context.ComponentTypes.FirstOrDefaultAsync(c => c.Id == ct.Id);
            if (existing == null)
            {
                context.ComponentTypes.Add(new ComponentType
                {
                    Id = ct.Id,
                    Index = ct.Index,
                    Name = ct.Name,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedDisplayModesAsync(ApplicationDbContext context)
    {
        var displayModes = new[]
        {
            new { Id = (int)DisplayModeId.Item, Index = DisplayModeIndex.Item, Name = "Item" },
            new { Id = (int)DisplayModeId.Grid, Index = DisplayModeIndex.Grid, Name = "Grid" },
            new { Id = (int)DisplayModeId.Carousel, Index = DisplayModeIndex.Carousel, Name = "Carousel" },
        };

        foreach (var dm in displayModes)
        {
            var existing = await context.DisplayModes.FirstOrDefaultAsync(d => d.Id == dm.Id);
            if (existing == null)
            {
                context.DisplayModes.Add(new DisplayMode
                {
                    Id = dm.Id,
                    Index = dm.Index,
                    Name = dm.Name,
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
        var wallPageType = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == "wall");

        if (homePageType == null || staticPageType == null)
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
                GradientSettings = FaceGradientPresets.JsonForFaceIndex("public"),
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
                GradientSettings = FaceGradientPresets.JsonForFaceIndex("basic"),
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
            };
            context.Faces.Add(basicFace);
            await context.SaveChangesAsync();

            // Pages for basic face (non-public: include Wall)
            var basicPages = new List<Page>
            {
                new Page { FaceId = basicFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
            };
            if (wallPageType != null)
            {
                basicPages.Add(new Page { FaceId = basicFace.Id, PageTypeId = wallPageType.Id, Name = "Wall", Path = "/wall", Index = 1, CreatedAt = DateTime.UtcNow });
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
                GradientSettings = FaceGradientPresets.JsonForFaceIndex("koncept"),
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
            };
            context.Faces.Add(konceptFace);
            await context.SaveChangesAsync();

            // Pages for koncept face (non-public: include Wall)
            var konceptPages = new List<Page>
            {
                new Page { FaceId = konceptFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
            };
            if (wallPageType != null)
            {
                konceptPages.Add(new Page { FaceId = konceptFace.Id, PageTypeId = wallPageType.Id, Name = "Wall", Path = "/wall", Index = 1, CreatedAt = DateTime.UtcNow });
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
        var faceUserRole = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceUser);
        var regularFaceRole = faceUserRole ?? faceHostRole;

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
                        IsActive = false,
                        Visited = false,
                        FaceRoleIntroCompleted = false,
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

                // Create UserFaceProfile and face role (FACE_USER for directory grid; fallback FACE_HOST)
                foreach (var face in faces)
                {
                    context.UserFaceProfiles.Add(new UserFaceProfile
                    {
                        UserProfileId = profile.Id,
                        FaceId = face.Id,
                        DisplayName = $"{firstName} {lastName}",
                        IsActive = false,
                        Visited = false,
                        FaceRoleIntroCompleted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                    if (regularFaceRole != null)
                    {
                        context.UserFaceRoles.Add(new UserFaceRole
                        {
                            UserId = user.Id,
                            FaceId = face.Id,
                            UserRoleId = regularFaceRole.Id,
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

    private const int GridDemoItemsPerUserPerFace = 5;

    /// <summary>
    /// Idempotent demo content: for each @demo.com user and each face, ensure N wall tickets, albums, blogs, reels, stories, chat rooms.
    /// Also migrates seeded user01–user30 from FACE_HOST to FACE_USER so face profile grids are populated.
    /// </summary>
    public static async Task SeedFaceGridContentAsync(ApplicationDbContext context)
    {
        await NormalizeDemoUserFaceRolesForProfileGridAsync(context);

        var faces = await context.Faces.AsNoTracking().ToListAsync();
        if (faces.Count == 0)
            return;

        // EndsWith(StringComparison) is not translatable; seeded demo emails use lowercase domain.
        var demoUserIds = await context.Users
            .AsNoTracking()
            .Where(u => u.Email != null && u.Email.EndsWith("@demo.com"))
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var userId in demoUserIds)
        {
            foreach (var face in faces)
            {
                await EnsureWallTicketsForUserFaceAsync(context, userId, face.Id);
                await EnsureAlbumsForUserFaceAsync(context, userId, face.Id);
                await EnsureBlogsForUserFaceAsync(context, userId, face.Id);
                await EnsureReelsForUserFaceAsync(context, userId, face.Id);
                await EnsureStoriesForUserFaceAsync(context, userId, face.Id);
                await EnsureChatRoomsForUserFaceAsync(context, userId, face.Id);
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine("✅ Face grid demo content seeded (idempotent)");
    }

    private static async Task NormalizeDemoUserFaceRolesForProfileGridAsync(ApplicationDbContext context)
    {
        var faceUser = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceUser);
        var faceHost = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceHost);
        if (faceUser == null || faceHost == null)
            return;

        // Avoid StartsWith/EndsWith(StringComparison): not translatable to SQL on all providers.
        var candidates = await context.Users
            .AsNoTracking()
            .Where(u => u.Email != null && u.Email.EndsWith("@demo.com"))
            .Select(u => new { u.Id, Email = u.Email! })
            .ToListAsync();
        var targetUserIds = candidates
            .Where(x => x.Email.StartsWith("user", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToList();

        if (targetUserIds.Count == 0)
            return;

        var toUpdate = await context.UserFaceRoles
            .Where(ufr => targetUserIds.Contains(ufr.UserId) && ufr.UserRoleId == faceHost.Id)
            .ToListAsync();

        foreach (var row in toUpdate)
            row.UserRoleId = faceUser.Id;

        if (toUpdate.Count > 0)
            await context.SaveChangesAsync();
    }

    private static async Task EnsureWallTicketsForUserFaceAsync(ApplicationDbContext context, string userId, int faceId)
    {
        var have = await context.FaceWallTickets.CountAsync(t => t.CreatorUserId == userId && t.FaceId == faceId);
        for (var i = have; i < GridDemoItemsPerUserPerFace; i++)
        {
            context.FaceWallTickets.Add(new FaceWallTicket
            {
                FaceId = faceId,
                CreatorUserId = userId,
                Title = $"Listing {i + 1} (face {faceId})",
                Description = $"Demo wall ticket from seeded user. Item index {i + 1} for face {faceId}.",
                Status = FaceWallTicketStatus.Active,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    private static async Task EnsureAlbumsForUserFaceAsync(ApplicationDbContext context, string userId, int faceId)
    {
        var have = await context.Albums.CountAsync(a =>
            a.CreatorId == userId && a.AlbumFaces.Any(af => af.FaceId == faceId));
        var toAdd = GridDemoItemsPerUserPerFace - have;
        if (toAdd <= 0)
            return;

        var albums = new List<Album>();
        for (var k = 0; k < toAdd; k++)
        {
            albums.Add(new Album
            {
                CreatorId = userId,
                Title = $"Album {have + k + 1} · face {faceId}",
                Description = "Seeded album for grid demo.",
                AlbumType = AlbumTypeEnum.Public,
                MediaType = MediaTypeEnum.Image,
                CreatedAt = DateTime.UtcNow,
            });
        }

        context.Albums.AddRange(albums);
        await context.SaveChangesAsync();

        foreach (var a in albums)
        {
            context.AlbumFaces.Add(new AlbumFace
            {
                AlbumId = a.Id,
                FaceId = faceId,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    private static async Task EnsureBlogsForUserFaceAsync(ApplicationDbContext context, string userId, int faceId)
    {
        var have = await context.Blogs.CountAsync(b => b.CreatorId == userId && b.FaceId == faceId);
        for (var i = have; i < GridDemoItemsPerUserPerFace; i++)
        {
            var created = DateTime.UtcNow;
            var blog = new Blog
            {
                CreatorId = userId,
                FaceId = faceId,
                Title = $"Blog post {i + 1} (face {faceId})",
                Content = $"Seeded blog body for grid demo. Face {faceId}, index {i + 1}.",
                CreatedAt = created,
                Images = new List<BlogImage>
                {
                    new BlogImage
                    {
                        ImageUrl = $"https://picsum.photos/seed/blog{faceId}{userId.GetHashCode()}{i}/640/400",
                        SortOrder = 0,
                        CreatedAt = created,
                    },
                },
            };
            context.Blogs.Add(blog);
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureReelsForUserFaceAsync(ApplicationDbContext context, string userId, int faceId)
    {
        var have = await context.Reels.CountAsync(r =>
            r.CreatorId == userId && r.ReelFaces.Any(rf => rf.FaceId == faceId));
        var toAdd = GridDemoItemsPerUserPerFace - have;
        if (toAdd <= 0)
            return;

        const string demoVideo =
            "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4";
        var reels = new List<Reel>();
        for (var k = 0; k < toAdd; k++)
        {
            reels.Add(new Reel
            {
                CreatorId = userId,
                Title = $"Reel {have + k + 1} (face {faceId})",
                Description = "Seeded reel for grid demo.",
                VideoUrl = demoVideo,
                CreatedAt = DateTime.UtcNow,
            });
        }

        context.Reels.AddRange(reels);
        await context.SaveChangesAsync();

        foreach (var r in reels)
        {
            context.ReelFaces.Add(new ReelFace
            {
                ReelId = r.Id,
                FaceId = faceId,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    private static async Task EnsureStoriesForUserFaceAsync(ApplicationDbContext context, string userId, int faceId)
    {
        var have = await context.Stories.CountAsync(s =>
            s.CreatorId == userId &&
            s.StoryFaces.Any(sf => sf.FaceId == faceId) &&
            s.State == StoryState.Published);
        for (var i = have; i < GridDemoItemsPerUserPerFace; i++)
        {
            var now = DateTime.UtcNow;
            var story = new Story
            {
                CreatorId = userId,
                Title = $"Story {i + 1} (face {faceId})",
                State = StoryState.Published,
                PublishedAt = now.AddMinutes(-10),
                ExpiresAt = now.AddDays(1),
                CreatedAt = now,
            };
            context.Stories.Add(story);
            await context.SaveChangesAsync();
            context.StoryFaces.Add(new StoryFace
            {
                StoryId = story.Id,
                FaceId = faceId,
                CreatedAt = now,
            });
            context.StoryImages.Add(new StoryImage
            {
                StoryId = story.Id,
                ImageUrl = $"https://picsum.photos/seed/story{faceId}{userId.GetHashCode()}{i}/400/700",
                SortOrder = 0,
                Description = "Cover",
                CreatedAt = now,
            });
        }
    }

    private static async Task EnsureChatRoomsForUserFaceAsync(ApplicationDbContext context, string userId, int faceId)
    {
        var have = await context.FaceChatRooms.CountAsync(r => r.CreatorUserId == userId && r.FaceId == faceId);
        for (var i = have; i < GridDemoItemsPerUserPerFace; i++)
        {
            context.FaceChatRooms.Add(new FaceChatRoom
            {
                FaceId = faceId,
                Title = $"Room {i + 1} (user slice)",
                Description = $"Seeded chat room for grid demo. Face {faceId}.",
                IsPublic = true,
                IsSystemManaged = false,
                CreatorUserId = userId,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }
}
