using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Services;

namespace BeDemo.Api.Tests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    // Static flag to ensure database is initialized only once across all test instances
    private static readonly object _dbInitLock = new object();
    private static bool _databaseInitialized = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing to skip database initialization
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContextOptions registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove ApplicationDbContext registration
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Use PostgreSQL for tests - use a single test database
            // Add MaxPoolSize to prevent "too many clients already" error
            var connectionString = "Host=localhost;Port=5432;Database=bedemo_test;Username=bedemo_user;Password=bedemo_password;MaxPoolSize=20;Connection Lifetime=0";
            
            // Add PostgreSQL database with connection pooling settings
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(30); // 30 second timeout
                });
                // Enable sensitive data logging only in test environment
                options.EnableSensitiveDataLogging();
            }, ServiceLifetime.Scoped);
            
            // Ensure MemoryCache is registered for RoutingMiddleware
            // MemoryCache is needed for caching faces in routing middleware
            if (!services.Any(s => s.ServiceType == typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache)))
            {
                services.AddMemoryCache();
            }
            
            // Ensure FaceService is registered for RoutingMiddleware
            // FaceService is needed by RoutingMiddleware to get faces from database
            if (!services.Any(s => s.ServiceType == typeof(IFaceService)))
            {
                services.AddScoped<IFaceService, FaceService>();
            }
            
            // Ensure fresh test database for each test run
            // Initialize database only once using static flag (thread-safe for parallel test execution)
            lock (_dbInitLock)
            {
                if (!_databaseInitialized)
                {
                    var serviceProvider = services.BuildServiceProvider();
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        try
                        {
                            // Always delete test database first to ensure fresh start
                            // This prevents test data from previous runs affecting current tests
                            context.Database.EnsureDeleted();
                            
                            // Create fresh database with all migrations applied
                            // This ensures database schema matches the latest migrations
                            context.Database.Migrate();
                            
                            // Seed UserRoles after migration (required for user creation)
                            // Use GetAwaiter().GetResult() since ConfigureWebHost is not async
                            BeDemo.Api.Scripts.DatabaseSeeder.SeedUserRolesAsync(context).GetAwaiter().GetResult();
                            
                            _databaseInitialized = true;
                            Console.WriteLine("✅ Test database initialized: bedemo_test (fresh, migrated, seeded)");
                        }
                        catch (Exception ex)
                        {
                            // If database setup fails, log the error but continue
                            // Connection might be temporarily unavailable, but we want to see the error
                            Console.WriteLine($"❌ Test database setup failed: {ex.Message}");
                            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                            
                            // Try once more with EnsureDeleted + Migrate
                            try
                            {
                                context.Database.EnsureDeleted();
                                context.Database.Migrate();
                                BeDemo.Api.Scripts.DatabaseSeeder.SeedUserRolesAsync(context).GetAwaiter().GetResult();
                                _databaseInitialized = true;
                                Console.WriteLine("✅ Test database initialized on retry: bedemo_test");
                            }
                            catch (Exception ex2)
                            {
                                Console.WriteLine($"❌ Test database setup retry also failed: {ex2.Message}");
                                // Don't throw - let tests run and fail with database errors if needed
                                // This helps identify connection issues
                            }
                        }
                    }
                }
            }
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }
}
