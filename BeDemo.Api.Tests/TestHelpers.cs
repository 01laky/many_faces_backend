using BeDemo.Api.Data;
using BeDemo.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BeDemo.Api.Tests;

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    public CapturingMailerWorkerClient CapturingMailer { get; } = new();
    // Static flag to ensure database is initialized only once across all test instances
    private static readonly object _dbInitLock = new object();
    private static bool _databaseInitialized = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing so Program.cs uses InMemory database (see Program.cs)
        builder.UseEnvironment("Testing");

        // Unique scope per factory instance: Program.cs prefixes rate-limit partition keys so parallel
        // WebApplicationFactory hosts (unlimited vs RateLimited* factories) do not share permit counters.
        builder.UseSetting("Testing:RateLimitScopeId", Guid.NewGuid().ToString("N"));

        builder.UseSetting("Mail:Enabled", "true");
        builder.UseSetting("Mail:WorkerGrpcUrl", "http://localhost:59998");
        builder.UseSetting("Uploads:SigningSecret", "test-upload-signing-secret-fixed-32b!!");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMailerWorkerClient>();
            services.AddSingleton<IMailerWorkerClient>(CapturingMailer);

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
                        IntegrationTestSeed.EnsureSuperAdminAsync(scope.ServiceProvider).GetAwaiter().GetResult();
                        IntegrationTestSeed.EnsureOperatorAiEnabledForIntegrationTestsAsync(scope.ServiceProvider)
                            .GetAwaiter().GetResult();
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

/// <summary>
/// Testing host with mail worker disabled (admin mailer test-self should return 400).
/// </summary>
public sealed class MailDisabledWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Mail:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMailerWorkerClient>();
            services.AddSingleton<IMailerWorkerClient, DisabledMailerWorkerClient>();
        });
    }
}

/// <summary>
/// Same in-memory test host as <see cref="CustomWebApplicationFactory{TProgram}"/> but disables the Testing
/// rate-limit bypass so OAuth endpoints return <strong>429</strong> after the configured permit burst
/// (see <see cref="OAuthRateLimit429Tests"/> and Program.cs ACL A21).
/// </summary>
public sealed class RateLimitedOAuthWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting merges into host configuration before Program reads rate-limit options (ConfigureAppConfiguration alone was too late for WebApplicationFactory).
        builder.UseSetting("OAuth2:BypassRateLimitInTesting", "false");
        builder.UseSetting("OAuth2:TokenRateLimitPermitLimit", "2");
        // Short windows so register + token 429 phases can run in one test (sleep between phases).
        builder.UseSetting("OAuth2:TokenRateLimitWindowSeconds", "3");
        builder.UseSetting("OAuth2:RegisterRateLimitPermitLimit", "2");
        builder.UseSetting("OAuth2:RegisterRateLimitWindowSeconds", "3");
        base.ConfigureWebHost(builder);
    }
}

/// <summary>
/// Test host for <see cref="LocalizationRateLimit429Tests"/>: enables real <c>localization-read</c> limits
/// with a tiny permit count so bursts return <strong>429</strong> without waiting a full production window.
/// </summary>
/// <remarks>
/// <para>
/// In <c>Testing</c> environment, <see cref="Program"/> normally sets permit limits to ~1M when
/// <c>OAuth2:BypassRateLimitInTesting</c> is true (default). Localization shares that bypass flag so
/// ordinary integration tests are not flaky; this factory sets the flag to <c>false</c> and lowers
/// <c>Localization:RateLimitPermitLimit</c> / window seconds.
/// </para>
/// </remarks>
public sealed class RateLimitedLocalizationWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("OAuth2:BypassRateLimitInTesting", "false");
        builder.UseSetting("Localization:RateLimitPermitLimit", "2");
        // Short window so sequential scenarios in LocalizationRateLimit429Tests can reset between phases.
        builder.UseSetting("Localization:RateLimitWindowSeconds", "3");
        base.ConfigureWebHost(builder);
    }
}
