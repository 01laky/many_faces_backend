using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BeDemo.Api.Data;

namespace BeDemo.Api.Tests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    // Static flag to ensure database is initialized only once across all test instances
    private static readonly object _dbInitLock = new object();
    private static bool _databaseInitialized = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing so Program.cs uses InMemory database (see Program.cs)
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Initialize in-memory database once per test run (Program.cs registers InMemory when Testing)
            lock (_dbInitLock)
            {
                if (!_databaseInitialized)
                {
                    var serviceProvider = services.BuildServiceProvider();
                    using (var scope = serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        context.Database.EnsureCreated();
                        BeDemo.Api.Scripts.DatabaseSeeder.SeedDataOnlyAsync(context).GetAwaiter().GetResult();
                        IntegrationTestSeed.EnsureAsync(scope.ServiceProvider).GetAwaiter().GetResult();
                        _databaseInitialized = true;
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

    /// <summary>
    /// HTTP client that prefixes requests with <c>/{face}/</c> for API and SignalR paths (see <see cref="FaceScopeTestHandler"/>).
    /// </summary>
    public HttpClient CreateFaceClient(string faceIndex = "public", WebApplicationFactoryClientOptions? options = null)
    {
        _ = options;
        return CreateDefaultClient(new FaceScopeTestHandler(faceIndex));
    }

    public new HttpClient CreateClient() => CreateFaceClient("public");

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options) => CreateFaceClient("public", options);

    /// <summary>Test server client without face prefix (for asserting legacy bare <c>/api/...</c> behavior).</summary>
    public HttpClient CreateUnscopedClient() => base.CreateClient();
}
