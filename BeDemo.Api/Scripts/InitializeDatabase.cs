using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BeDemo.Api.Data;
using BeDemo.Api.Models;

namespace BeDemo.Api.Scripts;

/// <summary>
/// Script to initialize database and create default admin user
/// </summary>
public static class DatabaseInitializer
{
	/// <summary>
	/// Initialize database and create admin user if it doesn't exist
	/// </summary>
	public static async Task InitializeAsync(IServiceProvider serviceProvider)
	{
		using var scope = serviceProvider.CreateScope();
		var services = scope.ServiceProvider;

		try
		{
			var context = services.GetRequiredService<ApplicationDbContext>();
			var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
			var configuration = services.GetRequiredService<IConfiguration>();
			var hostEnvironment = services.GetRequiredService<IHostEnvironment>();

			// Run migrations (this will create database if it doesn't exist)
			// MigrateAsync is safer than EnsureCreatedAsync when using migrations
			await context.Database.MigrateAsync();

			// Generate Mermaid ERD diagram after migrations
			var connectionString = configuration.GetConnectionString("DefaultConnection");
			if (!string.IsNullOrEmpty(connectionString))
			{
				await DatabaseDiagramGenerator.GenerateDiagramAsync(context, connectionString);
			}

			// Ensure UserRoles exist before creating users (SQL seeds may already have inserted them).
			if (ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(hostEnvironment, configuration))
				await DatabaseSeeder.SeedUserRolesAsync(context);

			// Get SUPER_ADMIN (global) role for admin user
			var superAdminRole = await context.UserRoles.FirstOrDefaultAsync(r => r.Name == UserRole.GlobalRoleNames.SuperAdmin);
			if (superAdminRole == null)
			{
				throw new InvalidOperationException("SUPER_ADMIN role not found. Please seed UserRoles first.");
			}

			// Check if admin user exists (by email)
			var adminUser = await userManager.FindByEmailAsync("admin@admin.com");
			if (adminUser == null)
			{
				// Create admin user
				// Set both UserName and Email to "admin@admin.com" so login works with email
				adminUser = new ApplicationUser
				{
					UserName = "admin@admin.com",
					Email = "admin@admin.com",
					EmailConfirmed = true,
					FirstName = "Admin",
					LastName = "User",
					CreatedAt = DateTime.UtcNow,
					UserRoleId = superAdminRole.Id // Assign SUPER_ADMIN role
				};

				// Temporarily remove password validators to allow simple "admin" password
				// This is only for the initial admin user - regular users still need strong passwords
				var validators = userManager.PasswordValidators.ToList();
				userManager.PasswordValidators.Clear();

				var result = await userManager.CreateAsync(adminUser, "admin");

				// Restore password validators
				foreach (var validator in validators)
				{
					userManager.PasswordValidators.Add(validator);
				}

				if (result.Succeeded)
				{
					// Ensure user is saved before creating UserProfile
					await context.SaveChangesAsync();

					// Create UserProfile for admin user (one-to-one relationship)
					var adminProfile = new UserProfile
					{
						UserId = adminUser.Id,
						Nickname = "Admin",
						Age = 30,
						Rod = "M",
						CreatedAt = DateTime.UtcNow
					};
					context.UserProfiles.Add(adminProfile);
					await context.SaveChangesAsync();

					Console.WriteLine("✅ Admin user created successfully!");
					Console.WriteLine("   Email: admin@admin.com");
					Console.WriteLine("   Password: admin");
					Console.WriteLine("   Profile ID: {0}", adminProfile.Id);
				}
				else
				{
					Console.WriteLine("❌ Failed to create admin user:");
					foreach (var error in result.Errors)
					{
						Console.WriteLine($"   - {error.Description}");
					}
				}
			}
			else
			{
				Console.WriteLine("ℹ️  Admin user already exists");

				// Ensure admin user has a UserProfile (in case it was created before UserProfile was added)
				var existingProfile = await context.UserProfiles.FirstOrDefaultAsync(up => up.UserId == adminUser.Id);
				if (existingProfile == null)
				{
					var adminProfile = new UserProfile
					{
						UserId = adminUser.Id,
						Nickname = "Admin",
						Age = 30,
						Rod = "M",
						CreatedAt = DateTime.UtcNow
					};
					context.UserProfiles.Add(adminProfile);
					await context.SaveChangesAsync();
					Console.WriteLine("✅ Created UserProfile for existing admin user (Profile ID: {0})", adminProfile.Id);
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error initializing database: {ex.Message}");
			throw;
		}
	}
}
