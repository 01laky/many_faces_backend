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
}
