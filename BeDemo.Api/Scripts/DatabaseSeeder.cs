using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.ProfileDetail;
using BeDemo.Api.Services;
using BeDemo.Api.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Scripts;

/// <summary>
/// Database seeder - seeds initial data for PageTypes, Faces, Pages, and Users
/// </summary>
public static class DatabaseSeeder
{
	/// <param name="seedReferenceDataViaApi">When false, reference data is assumed to come from external SQL (see <see cref="ReferenceSeedOptions"/>).</param>
	public static async Task SeedAsync(ApplicationDbContext context, bool seedReferenceDataViaApi = true)
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

		if (seedReferenceDataViaApi)
		{
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

			await SeedOAuthClientsAsync(context);
		}

		// Always run after external SQL reference seeds: PageType + one template page per face.
		await EnsureProfileDetailReferenceAsync(context);

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

		await SeedOAuthClientsAsync(context);

		await EnsureProfileDetailReferenceAsync(context);

		await context.SaveChangesAsync();
	}

	/// <summary>
	/// Idempotent: <see cref="ProfileDetailGridDefaults.PageTypeIndex"/> PageType + template Page per face.
	/// Runs even when reference data came from SQL seeds (docker dev).
	/// </summary>
	public static async Task EnsureProfileDetailReferenceAsync(ApplicationDbContext context)
	{
		await SeedProfileDetailPageTypeAsync(context);
		var service = new ProfileDetailTemplatePagesService(context);
		await service.EnsureAllFacesAsync();
	}

	private static async Task SeedProfileDetailPageTypeAsync(ApplicationDbContext context)
	{
		var index = ProfileDetailGridDefaults.PageTypeIndex;
		var existing = await context.PageTypes.FirstOrDefaultAsync(pt => pt.Index == index);
		if (existing == null)
		{
			context.PageTypes.Add(new PageType { Index = index, CreatedAt = DateTime.UtcNow });
		}
	}

	/// <summary>Default confidential client for demos; secret must match <c>OAuth2:ClientSecret</c> in appsettings (O1).</summary>
	public static async Task SeedOAuthClientsAsync(ApplicationDbContext context)
	{
		const string demoClientId = "be-demo-client";
		if (await context.OAuthClients.AnyAsync(c => c.ClientId == demoClientId))
			return;

		const string demoSecret = "be-demo-secret-very-strong-key";
		var hasher = new PasswordHasher<OAuthClient>();
		var entity = new OAuthClient
		{
			ClientId = demoClientId,
			IsActive = true,
			CreatedAtUtc = DateTime.UtcNow,
		};
		entity.SecretHash = hasher.HashPassword(entity, demoSecret);
		context.OAuthClients.Add(entity);
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
		// CMS-facing types: home, static, wall, profileDetail (member profile layout template per face).
		var pageTypeIndices = new[] { "home", "static", "wall", "profileDetail" };

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
			new { Id = (int)ComponentTypeId.VideoLounge, Index = ComponentTypeIndex.VideoLounge, Name = "Video Lounge" },
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

		// Face 4: admin — non-public platform scope for the admin SPA (URL prefix /admin/...).
		// Global Admin users manage all tenants from this scope; tenant URLs must not access other faces' data.
		var adminFace = await context.Faces.FirstOrDefaultAsync(f => f.Index == Utils.FaceScopeConstants.AdminFaceIndex);
		if (adminFace == null)
		{
			adminFace = new Face
			{
				Index = Utils.FaceScopeConstants.AdminFaceIndex,
				Title = "Admin",
				Description = "Administration scope (admin UI only)",
				GradientSettings = FaceGradientPresets.JsonForFaceIndex(Utils.FaceScopeConstants.AdminFaceIndex),
				IsPublic = false,
				CreatedAt = DateTime.UtcNow,
			};
			context.Faces.Add(adminFace);
			await context.SaveChangesAsync();

			var adminPages = new List<Page>
			{
				new Page { FaceId = adminFace.Id, PageTypeId = homePageType.Id, Name = "Home", Path = "/home", Index = 0, CreatedAt = DateTime.UtcNow },
			};
			if (wallPageType != null)
			{
				adminPages.Add(new Page { FaceId = adminFace.Id, PageTypeId = wallPageType.Id, Name = "Wall", Path = "/wall", Index = 1, CreatedAt = DateTime.UtcNow });
			}

			context.Pages.AddRange(adminPages);
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
		var faceAdminRole = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.FaceRoleNames.FaceAdmin);
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

				// Create UserFaceProfile and UserFaceRole for each face.
				// Demo admins get FaceAdmin on the admin scope face, FaceHost elsewhere (so they can open admin UI and tenants).
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
					var useFaceAdmin = faceAdminRole != null &&
						string.Equals(face.Index, Utils.FaceScopeConstants.AdminFaceIndex, StringComparison.OrdinalIgnoreCase);
					var perFaceRoleId = useFaceAdmin ? faceAdminRole!.Id : faceHostRole?.Id;
					if (perFaceRoleId != null)
					{
						context.UserFaceRoles.Add(new UserFaceRole
						{
							UserId = user.Id,
							FaceId = face.Id,
							UserRoleId = perFaceRoleId.Value,
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
	/// Idempotent demo content: for each @demo.com user and each face, ensure N wall tickets, albums, blogs, reels, stories, chat rooms, video lounges.
	/// Also migrates seeded user01–user30 from FACE_HOST to FACE_USER so face profile grids are populated.
	/// </summary>
	public static async Task SeedFaceGridContentAsync(ApplicationDbContext context)
	{
		await NormalizeDemoUserFaceRolesForProfileGridAsync(context);
		await EnsureDemoFaceSocialCreateFlagsAsync(context);

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
				await EnsureChatRoomsForUserFaceAsync(context, userId, face.Id, demoUserIds);
				await EnsureVideoLoungesForUserFaceAsync(context, userId, face.Id, demoUserIds);
			}
		}

		await context.SaveChangesAsync();
		await EnsureVideoLoungeLiveDemoSessionsAsync(context, demoUserIds);
		await EnsureSecondStoryImageForDemoStoriesAsync(context, demoUserIds);
		await ReactivateDemoExpiredStoriesAsync(context, demoUserIds);
		await EnsureOperatorStoryDetailSamplesAsync(context, demoUserIds);
		Console.WriteLine("✅ Face grid demo content seeded (idempotent)");
	}

	/// <summary>
	/// After <see cref="ReactivateDemoExpiredStoriesAsync"/>, seed draft/expired rows for operator story detail smoke (not revived).
	/// </summary>
	private static async Task EnsureOperatorStoryDetailSamplesAsync(ApplicationDbContext context, List<string> demoUserIds)
	{
		if (demoUserIds.Count == 0) return;

		var creatorId = demoUserIds[0];
		var faceId = await context.StoryFaces
			.Where(sf => sf.Story.CreatorId == creatorId)
			.Select(sf => sf.FaceId)
			.FirstOrDefaultAsync();
		if (faceId == 0)
		{
			faceId = await context.Faces.Select(f => f.Id).FirstOrDefaultAsync();
			if (faceId == 0) return;
		}

		var now = DateTime.UtcNow;

		if (!await context.Stories.AnyAsync(s => s.CreatorId == creatorId && s.Title.StartsWith("Operator sample draft")))
		{
			var draft = new Story
			{
				CreatorId = creatorId,
				Title = "Operator sample draft",
				State = StoryState.Draft,
				CreatedAt = now,
			};
			context.Stories.Add(draft);
			await context.SaveChangesAsync();
			context.StoryFaces.Add(new StoryFace { StoryId = draft.Id, FaceId = faceId, CreatedAt = now });
			context.StoryImages.Add(new StoryImage
			{
				StoryId = draft.Id,
				ImageUrl = $"https://picsum.photos/seed/op-draft-{draft.Id}/400/700",
				SortOrder = 0,
				CreatedAt = now,
			});
		}

		if (!await context.Stories.AnyAsync(s => s.CreatorId == creatorId && s.Title.StartsWith("Operator sample expired")))
		{
			var expired = new Story
			{
				CreatorId = creatorId,
				Title = "Operator sample expired",
				State = StoryState.Expired,
				PublishedAt = now.AddDays(-2),
				ExpiresAt = now.AddDays(-1),
				CreatedAt = now.AddDays(-3),
			};
			context.Stories.Add(expired);
			await context.SaveChangesAsync();
			context.StoryFaces.Add(new StoryFace { StoryId = expired.Id, FaceId = faceId, CreatedAt = now });
			context.StoryImages.Add(new StoryImage
			{
				StoryId = expired.Id,
				ImageUrl = $"https://picsum.photos/seed/op-expired-{expired.Id}/400/700",
				SortOrder = 0,
				CreatedAt = now,
			});
		}

		await context.SaveChangesAsync();
	}

	/// <summary>
	/// Demo stories seeded with one image get a second slide so FE hover slideshow has material (idempotent).
	/// </summary>
	private static async Task EnsureSecondStoryImageForDemoStoriesAsync(
		ApplicationDbContext context,
		List<string> demoUserIds)
	{
		if (demoUserIds.Count == 0) return;

		var stories = await context.Stories
			.Include(s => s.Images)
			.Where(s => demoUserIds.Contains(s.CreatorId))
			.ToListAsync();

		var added = 0;
		foreach (var story in stories)
		{
			if (story.Images.Count != 1) continue;
			context.StoryImages.Add(new StoryImage
			{
				StoryId = story.Id,
				ImageUrl = $"https://picsum.photos/seed/storyslide2-{story.Id}/400/700",
				SortOrder = 1,
				Description = "Slide 2",
				CreatedAt = DateTime.UtcNow,
			});
			added++;
		}

		if (added > 0)
		{
			await context.SaveChangesAsync();
			Console.WriteLine($"✅ Added second slide to {added} demo stor(y/ies) (hover slideshow)");
		}
	}

	/// <summary>
	/// Runs <see cref="ReactivateDemoExpiredStoriesAsync"/> using current @demo.com user ids.
	/// Called on API startup so listable stories stay visible even when <see cref="SeedFaceGridContentAsync"/> was skipped or failed earlier in the pipeline.
	/// </summary>
	public static async Task ReactivateExpiredStoriesForStartupAsync(ApplicationDbContext context)
	{
		var demoUserIds = await context.Users
			.AsNoTracking()
			.Where(u => u.Email != null && u.Email.EndsWith("@demo.com"))
			.Select(u => u.Id)
			.ToListAsync();
		await ReactivateDemoExpiredStoriesAsync(context, demoUserIds);
	}

	/// <summary>
	/// Demo: stories with elapsed TTL or <see cref="StoryState.Expired"/> become list-visible again (Published + far-future <see cref="Story.ExpiresAt"/>).
	/// In Production only creators in <paramref name="demoUserIds"/> are updated; in other environments all matching rows are updated.
	/// </summary>
	private static async Task ReactivateDemoExpiredStoriesAsync(ApplicationDbContext context, List<string> demoUserIds)
	{
		var now = DateTime.UtcNow;
		var until = now.AddYears(10);

		var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
				  ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
				  ?? "";
		var productionLike = env.Equals("Production", StringComparison.OrdinalIgnoreCase);

		var q = context.Stories.Where(s =>
			(s.State == StoryState.Published && s.ExpiresAt != null && s.ExpiresAt <= now) ||
			s.State == StoryState.Expired);

		if (productionLike)
		{
			if (demoUserIds.Count == 0)
				return;
			q = q.Where(s => demoUserIds.Contains(s.CreatorId));
		}

		var stories = await q.ToListAsync();

		foreach (var s in stories)
		{
			s.State = StoryState.Published;
			s.ExpiresAt = until;
			if (s.PublishedAt == null || s.PublishedAt > now)
				s.PublishedAt = now.AddMinutes(-10);
			s.UpdatedAt = now;
		}

		if (stories.Count > 0)
		{
			await context.SaveChangesAsync();
			Console.WriteLine($"✅ Reactivated {stories.Count} demo stor(y/ies) (extended ExpiresAt)");
		}
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

		if (toAdd > 0)
		{
			var albums = new List<Album>();
			for (var k = 0; k < toAdd; k++)
			{
				albums.Add(new Album
				{
					CreatorId = userId,
					Title = $"Album {have + k + 1} · face {faceId}",
					Description = AlbumDemoMediaSeedHelper.DemoAlbumDescription,
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

			await context.SaveChangesAsync();
		}

		// Same seed pass: attach 5–20 picsum images + 2 videos to every demo album on this face.
		var demoAlbumIds = await context.Albums
			.AsNoTracking()
			.Where(a =>
				a.CreatorId == userId
				&& a.Description == AlbumDemoMediaSeedHelper.DemoAlbumDescription
				&& a.AlbumFaces.Any(af => af.FaceId == faceId))
			.Select(a => a.Id)
			.ToListAsync();

		var mediaRefreshed = 0;
		foreach (var albumId in demoAlbumIds)
		{
			if (await AlbumDemoMediaSeedHelper.EnsureDemoMediaForAlbumAsync(context, albumId))
				mediaRefreshed++;
		}

		if (mediaRefreshed > 0)
			await context.SaveChangesAsync();
	}

	private static async Task EnsureBlogsForUserFaceAsync(ApplicationDbContext context, string userId, int faceId)
	{
		var have = await context.Blogs.CountAsync(b => b.CreatorId == userId && b.FaceId == faceId);
		for (var i = have; i < GridDemoItemsPerUserPerFace; i++)
		{
			var index = have + (i - have);
			var created = DateTime.UtcNow;
			// Demo mix: even index = no images; odd = 1–3 images; every 3rd = pending; every 5th = HTML; 15th = pending+HTML.
			var pendingDemo = index % 3 == 0 || index % 15 == 0;
			var withImages = index % 2 == 1;
			var htmlBody = index % 5 == 0 || index % 15 == 0;
			var content = htmlBody
				? $"<p>Seeded <strong>HTML</strong> body for face {faceId}, index {index + 1}.</p>"
				: $"Seeded blog body for grid demo. Face {faceId}, index {index + 1}.";

			var blog = new Blog
			{
				CreatorId = userId,
				FaceId = faceId,
				Title = $"Blog post {index + 1} (face {faceId})",
				Content = content,
				ApprovalStatus = pendingDemo
					? ContentApprovalStatus.PendingApproval
					: ContentApprovalStatus.Approved,
				AiReviewStatus = pendingDemo ? AiReviewStatus.NeedsHumanReview : AiReviewStatus.NotQueued,
				SubmittedAtUtc = pendingDemo ? created.AddMinutes(-index) : null,
				CreatedAt = created,
				Images = new List<BlogImage>(),
			};

			if (withImages)
			{
				var imageCount = (index % 3) + 1;
				for (var img = 0; img < imageCount; img++)
				{
					blog.Images.Add(new BlogImage
					{
						ImageUrl =
							$"https://picsum.photos/seed/blog{faceId}{userId.GetHashCode()}{index}{img}/640/400",
						SortOrder = img,
						CreatedAt = created,
					});
				}
			}

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
			var index = have + k;
			var pendingDemo = index % 3 == 0;
			reels.Add(new Reel
			{
				CreatorId = userId,
				Title = $"Reel {index + 1} (face {faceId})",
				Description = "Seeded reel for grid demo.",
				VideoUrl = demoVideo,
				ApprovalStatus = pendingDemo
					? ContentApprovalStatus.PendingApproval
					: ContentApprovalStatus.Approved,
				AiReviewStatus = pendingDemo ? AiReviewStatus.NeedsHumanReview : AiReviewStatus.NotQueued,
				SubmittedAtUtc = pendingDemo ? DateTime.UtcNow.AddMinutes(-index) : null,
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
				ExpiresAt = now.AddYears(10),
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
			context.StoryImages.Add(new StoryImage
			{
				StoryId = story.Id,
				ImageUrl = $"https://picsum.photos/seed/story2-{faceId}-{userId.GetHashCode()}-{i}/400/700",
				SortOrder = 1,
				Description = "Slide 2",
				CreatedAt = now,
			});
		}
	}

	/// <summary>Enables portal create panels for chat rooms and video lounges on tenant faces (not admin scope).</summary>
	private static async Task EnsureDemoFaceSocialCreateFlagsAsync(ApplicationDbContext context)
	{
		var faces = await context.Faces
			.Where(f => f.Index != Utils.FaceScopeConstants.AdminFaceIndex)
			.ToListAsync();

		foreach (var face in faces)
		{
			face.ChatRoomsCreate = true;
			face.VideoLoungesCreate = true;
		}

		await context.SaveChangesAsync();
	}

	private static async Task EnsureChatRoomsForUserFaceAsync(
		ApplicationDbContext context,
		string userId,
		int faceId,
		IReadOnlyList<string> demoUserIds)
	{
		var have = await context.FaceChatRooms.CountAsync(r => r.CreatorUserId == userId && r.FaceId == faceId);
		for (var k = 0; k < GridDemoItemsPerUserPerFace - have; k++)
		{
			var index = have + k;
			var isPublic = index % 2 == 0;
			var isSystem = index % 4 == 0;
			var description = index % 3 == 0 ? null : $"Seeded chat room for face {faceId}, index {index}.";

			var room = new FaceChatRoom
			{
				FaceId = faceId,
				Title = isSystem ? $"System room {index + 1}" : $"Room {index + 1} (user slice)",
				Description = description,
				IsPublic = isSystem || isPublic,
				IsSystemManaged = isSystem,
				CreatorUserId = isSystem ? null : userId,
				CreatedAt = DateTime.UtcNow,
			};
			context.FaceChatRooms.Add(room);
			await context.SaveChangesAsync();

			if (!isSystem)
			{
				context.FaceChatRoomMembers.Add(new FaceChatRoomMember
				{
					FaceChatRoomId = room.Id,
					UserId = userId,
					JoinedAt = DateTime.UtcNow,
				});
			}

			if (index % 2 == 0 && !isSystem)
			{
				var messageCount = (index % 3) + 2;
				DateTime? lastAt = null;
				for (var m = 0; m < messageCount; m++)
				{
					var sentAt = DateTime.UtcNow.AddMinutes(-messageCount + m);
					context.FaceChatRoomMessages.Add(new FaceChatRoomMessage
					{
						FaceChatRoomId = room.Id,
						SenderUserId = userId,
						Content = $"Seeded message {m + 1} in room {room.Id}",
						SentAt = sentAt,
					});
					lastAt = sentAt;
				}

				room.LastMessageAt = lastAt;
			}

			if (!isPublic && index % 3 == 1)
			{
				var otherUserId = demoUserIds.FirstOrDefault(id => id != userId);
				if (!string.IsNullOrEmpty(otherUserId))
				{
					context.FaceChatRoomJoinRequests.Add(new FaceChatRoomJoinRequest
					{
						FaceChatRoomId = room.Id,
						UserId = otherUserId,
						Status = FaceChatRoomJoinRequestStatus.Pending,
						CreatedAt = DateTime.UtcNow,
					});
				}
			}
		}
	}

	/// <summary>
	/// Idempotent: attach an active live session to first user-owned demo lounges (for Live badge) when missing.
	/// </summary>
	private static async Task EnsureVideoLoungeLiveDemoSessionsAsync(
		ApplicationDbContext context,
		IReadOnlyList<string> demoUserIds)
	{
		var candidateLounges = await context.FaceVideoLounges
			.Where(l => l.CreatorUserId != null && l.Title == "Lounge 2 (user slice)")
			.ToListAsync();

		foreach (var lounge in candidateLounges)
		{
			var hasLive = await context.FaceVideoLoungeSessions
				.AnyAsync(s => s.FaceVideoLoungeId == lounge.Id && s.EndedAt == null);
			if (hasLive)
				continue;

			var userId = lounge.CreatorUserId!;
			var session = new FaceVideoLoungeSession
			{
				FaceVideoLoungeId = lounge.Id,
				StartedByUserId = userId,
				StartedAt = DateTime.UtcNow.AddMinutes(-15),
				LastActivityAt = DateTime.UtcNow,
			};
			context.FaceVideoLoungeSessions.Add(session);
			await context.SaveChangesAsync();

			context.FaceVideoLoungeSessionParticipants.Add(new FaceVideoLoungeSessionParticipant
			{
				FaceVideoLoungeSessionId = session.Id,
				UserId = userId,
				JoinMode = VideoLoungeJoinMode.Full,
				AudioEnabled = true,
				VideoEnabled = true,
				IsListedInPublicRoster = true,
				LastSeenAt = DateTime.UtcNow,
			});

			var otherUserId = demoUserIds.FirstOrDefault(id => id != userId);
			if (!string.IsNullOrEmpty(otherUserId))
			{
				if (!await context.FaceVideoLoungeMembers.AnyAsync(
						m => m.FaceVideoLoungeId == lounge.Id && m.UserId == otherUserId))
				{
					context.FaceVideoLoungeMembers.Add(new FaceVideoLoungeMember
					{
						FaceVideoLoungeId = lounge.Id,
						UserId = otherUserId,
						JoinedAt = DateTime.UtcNow,
					});
				}

				context.FaceVideoLoungeSessionParticipants.Add(new FaceVideoLoungeSessionParticipant
				{
					FaceVideoLoungeSessionId = session.Id,
					UserId = otherUserId,
					JoinMode = VideoLoungeJoinMode.Viewer,
					AudioEnabled = false,
					VideoEnabled = false,
					IsListedInPublicRoster = true,
					LastSeenAt = DateTime.UtcNow,
				});
			}
		}

		await context.SaveChangesAsync();
	}

	/// <summary>
	/// Same cardinality as <see cref="EnsureChatRoomsForUserFaceAsync"/> — five lounges per demo user per face,
	/// with one active live session on the first user-owned lounge for grid Live badges.
	/// </summary>
	private static async Task EnsureVideoLoungesForUserFaceAsync(
		ApplicationDbContext context,
		string userId,
		int faceId,
		IReadOnlyList<string> demoUserIds)
	{
		var have = await context.FaceVideoLounges.CountAsync(r => r.CreatorUserId == userId && r.FaceId == faceId);
		for (var k = 0; k < GridDemoItemsPerUserPerFace - have; k++)
		{
			var index = have + k;
			var isPublic = index % 2 == 0;
			var isSystem = index % 4 == 0;
			var description = index % 3 == 0 ? null : $"Seeded video lounge for face {faceId}, index {index}.";

			var lounge = new FaceVideoLounge
			{
				FaceId = faceId,
				Title = isSystem ? $"System lounge {index + 1}" : $"Lounge {index + 1} (user slice)",
				Description = description,
				IsPublic = isSystem || isPublic,
				IsSystemManaged = isSystem,
				CreatorUserId = isSystem ? null : userId,
				MaxParticipants = 12,
				CreatedAt = DateTime.UtcNow,
			};
			context.FaceVideoLounges.Add(lounge);
			await context.SaveChangesAsync();

			if (!isSystem)
			{
				context.FaceVideoLoungeMembers.Add(new FaceVideoLoungeMember
				{
					FaceVideoLoungeId = lounge.Id,
					UserId = userId,
					JoinedAt = DateTime.UtcNow,
				});
			}

			// First user-owned lounge in each slice (index 1; index 0 is system): live session for "Live · N" badges.
			if (index == 1)
			{
				var session = new FaceVideoLoungeSession
				{
					FaceVideoLoungeId = lounge.Id,
					StartedByUserId = userId,
					StartedAt = DateTime.UtcNow.AddMinutes(-15),
					LastActivityAt = DateTime.UtcNow,
				};
				context.FaceVideoLoungeSessions.Add(session);
				await context.SaveChangesAsync();

				context.FaceVideoLoungeSessionParticipants.Add(new FaceVideoLoungeSessionParticipant
				{
					FaceVideoLoungeSessionId = session.Id,
					UserId = userId,
					JoinMode = VideoLoungeJoinMode.Full,
					AudioEnabled = true,
					VideoEnabled = true,
					IsListedInPublicRoster = true,
					LastSeenAt = DateTime.UtcNow,
				});

				var otherUserId = demoUserIds.FirstOrDefault(id => id != userId);
				if (!string.IsNullOrEmpty(otherUserId))
				{
					if (!await context.FaceVideoLoungeMembers.AnyAsync(
							m => m.FaceVideoLoungeId == lounge.Id && m.UserId == otherUserId))
					{
						context.FaceVideoLoungeMembers.Add(new FaceVideoLoungeMember
						{
							FaceVideoLoungeId = lounge.Id,
							UserId = otherUserId,
							JoinedAt = DateTime.UtcNow,
						});
					}

					context.FaceVideoLoungeSessionParticipants.Add(new FaceVideoLoungeSessionParticipant
					{
						FaceVideoLoungeSessionId = session.Id,
						UserId = otherUserId,
						JoinMode = VideoLoungeJoinMode.Viewer,
						AudioEnabled = false,
						VideoEnabled = false,
						IsListedInPublicRoster = true,
						LastSeenAt = DateTime.UtcNow,
					});
				}
			}

			if (!isPublic && index % 3 == 1)
			{
				var otherUserId = demoUserIds.FirstOrDefault(id => id != userId);
				if (!string.IsNullOrEmpty(otherUserId))
				{
					context.FaceVideoLoungeJoinRequests.Add(new FaceVideoLoungeJoinRequest
					{
						FaceVideoLoungeId = lounge.Id,
						UserId = otherUserId,
						Status = FaceVideoLoungeJoinRequestStatus.Pending,
						RequestedAt = DateTime.UtcNow,
					});
				}
			}
		}
	}
}
