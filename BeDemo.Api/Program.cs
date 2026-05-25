/*
 * Program.cs - Main entry point for ASP.NET Core application
 * 
 * This file configures all services, middleware and HTTP pipeline for BeDemo API.
 * Contains configuration for:
 * - Entity Framework Core with PostgreSQL database
 * - ASP.NET Core Identity for user authentication and authorization
 * - OAuth2 services with ECDSA signing of JWT tokens
 * - JWT Bearer authentication for API and SignalR
 * - SignalR for real-time WebSocket communication
 * - Swagger/OpenAPI documentation
 */

using BeDemo.Api.Utils;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Security;
using BeDemo.Api.Middlewares;
using BeDemo.Api.Services;
using BeDemo.Api.Services.OperatorAi;
using BeDemo.Api.Services.Search;
using BeDemo.Api.Configuration;
using BeDemo.Api.Hubs;
using BeDemo.Api.Scripts;
using BeDemo.Api.Swagger;
using Serilog;
using Grpc.Net.Client;
using StackExchange.Redis;

// Check for generate-diagram command line argument
if (args.Length > 0 && args[0] == "generate-diagram")
{
    // Quick test to generate diagram
    var diagramConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    if (string.IsNullOrWhiteSpace(diagramConnStr))
    {
        Console.Error.WriteLine("Set ConnectionStrings__DefaultConnection before running generate-diagram.");
        return;
    }
    var diagramOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(diagramConnStr)
        .Options;

    using var diagramContext = new ApplicationDbContext(diagramOptions);
    await BeDemo.Api.Scripts.DatabaseDiagramGenerator.GenerateDiagramAsync(diagramContext, diagramConnStr);
    return; // Exit after generating diagram
}

// Creates WebApplicationBuilder, which is used to configure the application
var builder = WebApplication.CreateBuilder(args);

var isHardenedEnv = builder.Environment.IsEnvironment("Hardened");
var isProductionEnv = builder.Environment.IsProduction();
var useTransportHardening = isProductionEnv || isHardenedEnv;

if (useTransportHardening)
{
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// gRPC to many_faces_elastic search-worker, many_faces_push, mailer, and AI may use cleartext HTTP/2 (h2c) when URL is http://.
var searchWorkerGrpcUrl = builder.Configuration["Search:WorkerGrpcUrl"] ?? string.Empty;
var pushWorkerGrpcUrl = builder.Configuration["Push:WorkerGrpcUrl"] ?? string.Empty;
var mailWorkerGrpcUrl = builder.Configuration["Mail:WorkerGrpcUrl"] ?? string.Empty;
var aiWorkerGrpcUrl = builder.Configuration["AiService:GrpcAddress"]
    ?? Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
    ?? string.Empty;
if (searchWorkerGrpcUrl.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase)
    || pushWorkerGrpcUrl.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase)
    || mailWorkerGrpcUrl.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase)
    || aiWorkerGrpcUrl.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase))
{
    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
}

// Development HTTPS: shared cert from dev/generate-https-certs.sh (repo dev/certs/localhost.pfx) or ASPNETCORE_DEV_HTTPS_PFX (e.g. Docker /https-certs/localhost.pfx).
if (builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    var envPfx = Environment.GetEnvironmentVariable("ASPNETCORE_DEV_HTTPS_PFX");
    var repoPfx = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "dev", "certs", "localhost.pfx"));
    var pfxPath = !string.IsNullOrWhiteSpace(envPfx) ? envPfx.Trim() : repoPfx;
    if (File.Exists(pfxPath))
    {
        // Avoid double-binding the same ports from launchSettings / ASPNETCORE_URLS.
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, string.Empty);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(8000, listen => listen.Protocols = HttpProtocols.Http1);
            options.ListenAnyIP(8001, listen =>
            {
                listen.Protocols = HttpProtocols.Http1AndHttp2;
                // macOS does not support EphemeralKeySet for PKCS#12 load (PlatformNotSupportedException).
                var keyFlags = OperatingSystem.IsMacOS()
                    ? X509KeyStorageFlags.DefaultKeySet
                    : X509KeyStorageFlags.EphemeralKeySet;
                listen.UseHttps(X509CertificateLoader.LoadPkcs12FromFile(
                    pfxPath,
                    string.Empty,
                    keyFlags));
            });
        });
    }
}

builder.Services.AddScoped<IPlatformStatsQueryService, PlatformStatsQueryService>();
builder.Services.AddOptions<BeDemo.Api.Configuration.OperatorAiOptions>()
    .BindConfiguration(BeDemo.Api.Configuration.OperatorAiOptions.SectionName);
builder.Services.AddScoped<IOperatorAiConversationService, OperatorAiConversationService>();
builder.Services.AddScoped<IOperatorAiEntityBundleLoader, OperatorAiEntityBundleLoader>();
builder.Services.AddScoped<IOperatorAiLiveStatsPrefetcher, OperatorAiLiveStatsPrefetcher>();
builder.Services.AddScoped<IOperatorAiLiveStatsOrchestrator, OperatorAiLiveStatsOrchestrator>();
builder.Services.AddScoped<IFaceModerationService, FaceModerationService>();
builder.Services.AddScoped<IOperatorUserModerationService, OperatorUserModerationService>();
builder.Services.AddScoped<IOperatorAlbumManagementService, OperatorAlbumManagementService>();
builder.Services.AddScoped<IOperatorReelManagementService, OperatorReelManagementService>();
builder.Services.AddScoped<IOperatorBlogManagementService, OperatorBlogManagementService>();
builder.Services.AddScoped<IOperatorStoryManagementService, OperatorStoryManagementService>();
builder.Services.AddScoped<IOperatorChatRoomManagementService, OperatorChatRoomManagementService>();
builder.Services.AddScoped<IOperatorProfileSocialManagementService, OperatorProfileSocialManagementService>();
builder.Services.AddScoped<IPlatformDirectMessageService, PlatformDirectMessageService>();
builder.Services.AddScoped<IOperatorUserChatService, OperatorUserChatService>();
builder.Services.AddSingleton<IPlatformChatRateLimiter, PlatformChatRateLimiter>();
builder.Services.AddScoped<IChatRoomLifecycleService, ChatRoomLifecycleService>();
builder.Services.Configure<BeDemo.Api.Configuration.VideoLoungeOptions>(
    builder.Configuration.GetSection(BeDemo.Api.Configuration.VideoLoungeOptions.SectionName));
builder.Services.AddScoped<IVideoLoungeTokenService, VideoLoungeTokenService>();
builder.Services.AddScoped<IVideoLoungeLifecycleService, VideoLoungeLifecycleService>();
// User-generated content moderation: AI job worker, dashboard metrics, in-app notifications, and optional retention cleanup.
builder.Services.AddScoped<IContentAiReviewService, ContentAiReviewService>();
builder.Services.AddScoped<IContentModerationMetrics, ContentModerationMetrics>();
builder.Services.AddScoped<IContentModerationNotifier, ContentModerationNotifier>();
builder.Services.AddScoped<IContentRetentionCleanupService, ContentRetentionCleanupService>();
builder.Services.AddHostedService<ContentRetentionHostedService>();
builder.Services.Configure<ContentModerationSecurityOptions>(
    builder.Configuration.GetSection(ContentModerationSecurityOptions.SectionName));
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.SectionName));
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.SectionName));
builder.Services.Configure<RegistrationInviteOptions>(builder.Configuration.GetSection(RegistrationInviteOptions.SectionName));
builder.Services.Configure<MailRegistrationLinkOptions>(builder.Configuration.GetSection(MailRegistrationLinkOptions.SectionName));
builder.Services.AddScoped<IRegistrationInviteService, RegistrationInviteService>();
builder.Services.AddScoped<IUserRegistrationProvisioner, UserRegistrationProvisioner>();
builder.Services.AddHostedService<RegistrationInviteCleanupHostedService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ISearchWorkerProbe, SearchWorkerGrpcProbe>();
builder.Services.AddSingleton<ISearchQueryGateway, SearchWorkerGrpcGateway>();
builder.Services.AddScoped<ISearchOutboxService, SearchOutboxService>();
builder.Services.AddScoped<SearchDocumentBuilder>();
builder.Services.AddScoped<SearchHitAclFilter>();
builder.Services.AddScoped<IAdminSearchAutocompleteService, AdminSearchAutocompleteService>();
builder.Services.AddScoped<SearchIndexReconciliationRunner>();

var searchOptionsForHosted = builder.Configuration.GetSection(SearchOptions.SectionName).Get<SearchOptions>() ?? new SearchOptions();
if (searchOptionsForHosted.IsEnabled)
{
    builder.Services.AddHostedService<SearchOutboxProcessorHostedService>();
    if (searchOptionsForHosted.ReconciliationEnabled)
        builder.Services.AddHostedService<SearchIndexReconciliationHostedService>();
}

builder.Services.AddSingleton<IPushWorkerClient, PushWorkerGrpcClient>();
builder.Services.AddSingleton<IMailerWorkerClient, MailerWorkerGrpcClient>();

// Configure Serilog for structured logging
// Serilog provides better logging capabilities than default .NET logging
// It supports structured logging (logging with properties) and multiple sinks (outputs)
// Seq server URL can be overridden via environment variable: Serilog__WriteTo__1__Args__serverUrl
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)  // Reads Serilog configuration from appsettings.json
    .Enrich.FromLogContext()                        // Enriches logs with context information (request ID, user, etc.)
    .Enrich.WithMachineName()                       // Adds machine name to logs
    .Enrich.WithEnvironmentName()                   // Adds environment name (Development, Production, etc.)
    .WriteTo.Console()                              // Writes logs to console (stdout)
    .WriteTo.Seq(                                    // Writes logs to Seq server (structured logging server with web UI)
        serverUrl: Environment.GetEnvironmentVariable("Serilog__WriteTo__1__Args__serverUrl")
            ?? builder.Configuration["Serilog:WriteTo:1:Args:serverUrl"]
            ?? "http://seq:5341",                    // Default Seq URL (works in Docker network)
        apiKey: null)                                // No API key needed for local development
    .CreateLogger();

// Replaces default .NET logging with Serilog
// This ensures all logs go through Serilog and can be sent to Seq
builder.Host.UseSerilog();

// ============================================================================
// SERVICE CONFIGURATION
// ============================================================================

// Adds support for MVC controllers - enables creating REST API endpoints
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Endpoint schema validation: FluentValidation auto 400 ProblemDetails (§6); validators in BeDemo.Api/Validation.
builder.Services.AddFluentValidationAutoValidation(options =>
{
    // Token endpoint maps failures to OAuth2ErrorResponse via manual ValidateAsync (§6).
    options.Filter = type => type != typeof(BeDemo.Api.Models.DTOs.OAuth2TokenRequest);
});
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddScoped<BeDemo.Api.Validation.Files.IFileValidator, BeDemo.Api.Validation.Files.FileValidator>();

// Adds support for OpenAPI/Swagger - automatic API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" });
    // ACL A23: Bearer scheme + per-operation security for [Authorize] via BearerAuthOperationFilter; exempt routes stay callable without Authorize in Swagger UI.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description =
            "OAuth2 password grant at POST /api/oauth2/token. Most /api/* operations need Authorization: Bearer {access_token}. " +
            "Use face-prefixed URLs /{face}/api/... in browsers; exempt: /api/oauth2/*, /api/auth/*.",
    });
    c.OperationFilter<BearerAuthOperationFilter>();
});

// ACL A21: fixed-window rate limits on token + register. In Testing, limits are bypassed unless OAuth2:BypassRateLimitInTesting=false (for 429 tests).
var isTestingEnv = builder.Environment.IsEnvironment("Testing");
var bypassRateLimitInTesting = builder.Configuration.GetValue("OAuth2:BypassRateLimitInTesting", true);
var oauthPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("OAuth2:TokenRateLimitPermitLimit", 60);
var oauthWindowSec = builder.Configuration.GetValue("OAuth2:TokenRateLimitWindowSeconds", 60);
var registerPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("OAuth2:RegisterRateLimitPermitLimit", 30);
var registerWindowSec = builder.Configuration.GetValue("OAuth2:RegisterRateLimitWindowSeconds", 60);
// localization-read: anonymous GET /api/localization/{app} on every SPA cold load.
// Shares OAuth2:BypassRateLimitInTesting in Testing so most integration tests stay unlimited;
// LocalizationRateLimit429Tests sets BypassRateLimitInTesting=false and low Localization:* limits.
var localizationPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("Localization:RateLimitPermitLimit", 120);
var localizationWindowSec = builder.Configuration.GetValue("Localization:RateLimitWindowSeconds", 60);
var authLoginPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("Auth:LoginRateLimitPermitLimit", 30);
var authLoginWindowSec = builder.Configuration.GetValue("Auth:LoginRateLimitWindowSeconds", 60);
var apiGlobalPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("RateLimit:ApiPermitLimit", 600);
var apiGlobalWindowSec = builder.Configuration.GetValue("RateLimit:ApiWindowSeconds", 60);
var uploadPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("RateLimit:UploadPermitLimit", 20);
var uploadWindowSec = builder.Configuration.GetValue("RateLimit:UploadWindowSeconds", 60);
var registerPrefillPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("RateLimit:RegisterPrefillPermitLimit", 30);
var registerPrefillWindowSec = builder.Configuration.GetValue("RateLimit:RegisterPrefillWindowSeconds", 60);
var aiAvailabilityPermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("RateLimit:AiAvailabilityPermitLimit", 120);
var aiAvailabilityWindowSec = builder.Configuration.GetValue("RateLimit:AiAvailabilityWindowSeconds", 60);
var signalrNegotiatePermit = isTestingEnv && bypassRateLimitInTesting
    ? 1_000_000
    : builder.Configuration.GetValue("RateLimit:SignalrNegotiatePermitLimit", 60);
var signalrNegotiateWindowSec = builder.Configuration.GetValue("RateLimit:SignalrNegotiateWindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        await ctx.HttpContext.Response.WriteAsync(
            "{\"error\":\"rate_limit\",\"error_description\":\"Too many requests. See Retry-After.\"}",
            ct);
    };
    options.AddPolicy("oauth-token", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = oauthPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, oauthWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.AddPolicy("oauth-register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = registerPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, registerWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    // Static i18n bundles: separate policy from oauth-* so tuning does not affect login/register.
    options.AddPolicy("localization-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = localizationPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, localizationWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authLoginPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, authLoginWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.AddPolicy("api-global", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = apiGlobalPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, apiGlobalWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.AddPolicy("upload-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = uploadPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, uploadWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.AddPolicy("oauth-register-prefill", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = registerPrefillPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, registerPrefillWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.AddPolicy("ai-availability-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = aiAvailabilityPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, aiAvailabilityWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
    options.AddPolicy("signalr-negotiate", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitingPartitionKey.ForHttpContext(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = signalrNegotiatePermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, signalrNegotiateWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // BSH3-A4: default API throttle — skipped when endpoint has its own [EnableRateLimiting] policy.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>() != null
            || endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>() != null)
        {
            return RateLimitPartition.GetNoLimiter(RateLimitingPartitionKey.ForHttpContext(httpContext));
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            RateLimitingPartitionKey.ForHttpContext(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = apiGlobalPermit,
                Window = TimeSpan.FromSeconds(Math.Max(1, apiGlobalWindowSec)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });
});

// ============================================================================
// MEMORY CACHE CONFIGURATION
// ============================================================================

// Adds in-memory caching - used for caching faces in routing middleware
// Cache improves performance by avoiding database queries on every request
// Faces are cached for 5 minutes and automatically refreshed
builder.Services.AddMemoryCache();
builder.Services.AddLocalization();
builder.Services.AddSingleton<BeDemo.Api.Services.ILocalizationBundleService, BeDemo.Api.Services.LocalizationBundleService>();

// ============================================================================
// CORS CONFIGURATION
// ============================================================================

// CORS: default dev origins + optional Cors:Origins[] from configuration (production).
var defaultCorsOrigins = new[]
{
    "http://localhost:8081", "http://localhost:8082", "http://localhost:8080", "http://localhost:9080",
    "http://localhost:9081", "https://localhost:8081", "https://localhost:8082", "https://localhost:8080",
    "https://localhost:9080", "https://localhost:9081",
};
var extraOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
var devLanHost = builder.Configuration["DEV_LAN_HOST"]?.Trim();
if (string.IsNullOrEmpty(devLanHost))
    devLanHost = Environment.GetEnvironmentVariable("DEV_LAN_HOST")?.Trim();
var lanOrigins = BeDemo.Api.Dev.DevLanCorsOriginBuilder.Build(devLanHost);
var corsOriginSource = useTransportHardening
    ? extraOrigins
    : defaultCorsOrigins.Concat(lanOrigins).Concat(extraOrigins);
var corsOrigins = corsOriginSource
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// ============================================================================
// DATABASE CONFIGURATION (Entity Framework Core)
// ============================================================================

if (builder.Environment.IsEnvironment("Testing"))
{
    // In-memory database for integration tests (no PostgreSQL required)
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("BeDemoTestDb"));
    builder.Services.AddDbContextFactory<ApplicationDbContext>(
        options => options.UseInMemoryDatabase("BeDemoTestDb"),
        ServiceLifetime.Scoped);
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    var connectionStringForLogging = connectionString.Contains("Password=")
        ? connectionString.Substring(0, connectionString.IndexOf("Password=")) + "Password=***"
        : connectionString;
    Log.Information("Using connection string: {ConnectionString}", connectionStringForLogging);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
    builder.Services.AddDbContextFactory<ApplicationDbContext>(
        options => options.UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)),
        ServiceLifetime.Scoped);
}

// FaceService for RoutingMiddleware (face-based URL routing)
builder.Services.AddScoped<IFaceService, FaceService>();
builder.Services.AddScoped<IProfileDetailTemplatePagesService, ProfileDetailTemplatePagesService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFaceScopeContext, FaceScopeContext>();
builder.Services.AddScoped<IAccessEvaluator, AccessEvaluator>();
builder.Services.AddScoped<IAccessCapabilitiesService, AccessCapabilitiesService>();
builder.Services.AddScoped<IOAuthRefreshTokenStore, OAuthRefreshTokenStore>();
builder.Services.AddSingleton<IChatHubAiRateLimiter, ChatHubAiRateLimiter>();
builder.Services.AddScoped<IStoryLifecycleService, StoryLifecycleService>();
builder.Services.AddScoped<IFaceWallTicketLifecycleService, FaceWallTicketLifecycleService>();

// ============================================================================
// ASP.NET CORE IDENTITY CONFIGURATION
// ============================================================================

// SHV2 BE-A3: minimum password length from Identity:Password:RequiredLength (12 default; 4 allowed in Development only).
builder.Services.AddOptions<BeDemo.Api.Configuration.IdentityPasswordPolicyOptions>()
    .BindConfiguration(BeDemo.Api.Configuration.IdentityPasswordPolicyOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<
    Microsoft.Extensions.Options.IValidateOptions<BeDemo.Api.Configuration.IdentityPasswordPolicyOptions>,
    BeDemo.Api.Configuration.IdentityPasswordPolicyValidateOptions>();
builder.Services.AddSingleton<
    Microsoft.Extensions.Options.IPostConfigureOptions<Microsoft.AspNetCore.Identity.IdentityOptions>,
    BeDemo.Api.Configuration.ConfigureIdentityPasswordPolicy>();

// Adds ASP.NET Core Identity framework for user, role and authentication management
// Identity automatically creates necessary database tables (Users, Roles, UserRoles, etc.)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password complexity (minimum length applied via ConfigureIdentityPasswordPolicy from config).
    options.Password.RequireDigit = true;              // Password must contain at least one digit
    options.Password.RequireLowercase = true;           // Password must contain at least one lowercase letter
    options.Password.RequireUppercase = true;           // Password must contain at least one uppercase letter
    options.Password.RequireNonAlphanumeric = true;    // Password must contain at least one special character

    // User settings
    options.User.RequireUniqueEmail = true;             // Email must be unique

    // Lockout settings (account blocking after failed attempts)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);  // Account is locked for 5 minutes
    options.Lockout.MaxFailedAccessAttempts = 5;        // Account is locked after 5 failed attempts
    options.Lockout.AllowedForNewUsers = true;          // Lockout applies to new users as well
})
.AddEntityFrameworkStores<ApplicationDbContext>()       // Uses Entity Framework Core as storage for Identity
.AddDefaultTokenProviders();                            // Adds default token providers (e.g., for email confirmation)

// ============================================================================
// OAUTH2 AND JWT AUTHENTICATION CONFIGURATION
// ============================================================================

// Singleton ECDSA key for JWT (ES512). Optional Jwt:SigningPemPath for stable keys; see ECDSAKeyService.
var ecdsaKeyService = new ECDSAKeyService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IECDSAKeyService>(ecdsaKeyService);

builder.Services.AddScoped<IPasswordHasher<OAuthClient>, PasswordHasher<OAuthClient>>();

builder.Services.AddSingleton<IClock, SystemUtcClock>();

// SHV2 BE-A2: cap remember-me access JWT at 7 days; reject legacy multi-year ExpiresInMinutesRememberMe at startup.
builder.Services.AddOptions<BeDemo.Api.Configuration.JwtTokenLifetimeOptions>()
    .BindConfiguration(BeDemo.Api.Configuration.JwtTokenLifetimeOptions.SectionName)
    .Validate(
        o => o.ExpiresInMinutes > 0 &&
             o.ExpiresInMinutesRememberMe > 0 &&
             o.ExpiresInMinutesRememberMe <= BeDemo.Api.Configuration.JwtTokenLifetimeOptions.MaxRememberMeAccessMinutes &&
             o.ExpiresInMinutesRememberMe >= o.ExpiresInMinutes,
        $"Jwt:{nameof(BeDemo.Api.Configuration.JwtTokenLifetimeOptions.ExpiresInMinutesRememberMe)} must be " +
        $"{BeDemo.Api.Configuration.JwtTokenLifetimeOptions.RecommendedRememberMeAccessMinutes} minutes (7 days) or less, " +
        "and not less than Jwt:ExpiresInMinutes. Remove legacy values like " +
        $"{BeDemo.Api.Configuration.JwtTokenLifetimeOptions.LegacyMisconfiguredRememberMeMinutes}.")
    .ValidateOnStart();

builder.Services.AddScoped<IOAuthClientValidator, OAuthClientValidator>();
builder.Services.AddScoped<IOAuthTokenRequestSignatureVerifier, OAuthTokenRequestSignatureVerifier>();
builder.Services.AddScoped<IOAuthAccessTokenFactory, OAuthAccessTokenFactory>();
builder.Services.AddScoped<IOAuth2Service, OAuth2Service>();

// SHV2 BE-U3 — HMAC-signed URLs for wwwroot/uploads (replaces public static /uploads/*).
builder.Services.AddOptions<BeDemo.Api.Configuration.UploadsOptions>()
    .BindConfiguration(BeDemo.Api.Configuration.UploadsOptions.SectionName)
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.SigningSecret) && o.SigningSecret.Length >= 32,
        $"Uploads:{nameof(BeDemo.Api.Configuration.UploadsOptions.SigningSecret)} must be at least 32 characters.")
    .ValidateOnStart();
builder.Services.AddSingleton<IUploadSignedUrlService, UploadSignedUrlService>();

builder.Services.AddOptions<HardenedSecurityOptions>()
    .BindConfiguration(HardenedSecurityOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<HardenedSecurityOptions>, HardenedSecurityValidateOptions>();

// AI gRPC client - singleton to reuse the HTTP/2 channel across requests
builder.Services.AddOptions<AiServiceOptions>()
    .BindConfiguration(AiServiceOptions.SectionName);
builder.Services.AddSingleton<AiGrpcService>();
builder.Services.AddSingleton<IAiModelStatusClient>(sp => sp.GetRequiredService<AiGrpcService>());
builder.Services.AddSingleton<IAiGrpcService, AiAvailabilityGuardGrpcService>();
builder.Services.AddScoped<IAiWorkerHostProfileService, AiWorkerHostProfileService>();
builder.Services.AddHostedService<AiWorkerHostProfileStartupRefresh>();

// Gets signing key from ECDSAKeyService - this key is used to sign JWT tokens
var signingKey = ecdsaKeyService.GetSigningKey();

// Configures JWT Bearer authentication - used to protect API endpoints and SignalR hubs
builder.Services.AddAuthentication(options =>
{
    // Sets JWT Bearer as default authentication scheme
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // JWT token validation configuration
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKeys = ecdsaKeyService.GetIssuerSigningKeys().ToList(),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "BeDemoApi",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "BeDemoApi",
        ValidateLifetime = true,
        // Zero skew: rely on NTP in production; document in docs/guides/authentication-and-sessions.md (J2).
        ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha512 },
    };

    // SignalR: Bearer in query (WSS). J6: after signature validation, require atv == user.AccessTokenVersion.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                context.Token = accessToken;
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Auth.JwtBearer");
            logger.LogWarning(
                context.Exception,
                "authFailureReason=jwt_validation_failure path={Path}",
                context.HttpContext.Request.Path);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Auth.JwtBearer");
            logger.LogWarning(
                "authFailureReason=jwt_challenge path={Path} error={Error}",
                context.HttpContext.Request.Path,
                context.Error ?? "none");
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return;
            var versionClaim = context.Principal?.FindFirstValue(BeDemoClaimTypes.AccessTokenVersion);
            var claimed = int.TryParse(versionClaim, out var v) ? v : 0;
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null)
            {
                context.Fail("User no longer exists.");
                return;
            }

            if (user.AccessTokenVersion != claimed)
                context.Fail("Access token revoked (session version mismatch).");
        },
    };
});

// BSH3-A1: default deny — explicit [AllowAnonymous] on OAuth, JWKS, localization, documented public routes.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ============================================================================
// SIGNALR CONFIGURATION
// ============================================================================

// Adds SignalR services - enables real-time bidirectional communication via WebSocket
// SignalR automatically manages connections, reconnect logic and fallback to Long Polling if WebSocket is not available
builder.Services.AddSignalR();

// ============================================================================
// OPENAPI CONFIGURATION
// ============================================================================

// Adds OpenAPI support - generates automatic API documentation
builder.Services.AddOpenApi();

// ============================================================================
// REDIS JOB QUEUE (BullMQ-style: list + delayed sorted set)
// ============================================================================

builder.Services.Configure<RedisJobWorkerOptions>(
    builder.Configuration.GetSection("RedisJobWorker"));

var redisConfiguration = builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redisConfiguration) && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConfiguration));
    builder.Services.AddSingleton<IRedisJobQueue, RedisJobQueue>();
    builder.Services.AddSingleton<IOperatorAiRedisStringStore, StackExchangeOperatorAiRedisStringStore>();
    builder.Services.AddSingleton<IOperatorAiBundleRedisCache, OperatorAiBundleRedisCache>();
    builder.Services.AddHostedService<RedisJobWorkerService>();
}
else
{
    builder.Services.AddSingleton<IRedisJobQueue, NoOpRedisJobQueue>();
    builder.Services.AddSingleton<IOperatorAiBundleRedisCache, NoOpOperatorAiBundleRedisCache>();
}

builder.Services.AddSingleton<IOperatorAiLiveStatsCacheSettingsProvider, OperatorAiLiveStatsCacheSettingsService>();
builder.Services.AddSingleton<IOperatorAiPublicStatsSettingsProvider, OperatorAiPublicStatsSettingsService>();
builder.Services.AddSingleton<IOperatorAiSystemSettingsProvider, OperatorAiSystemSettingsService>();
builder.Services.AddScoped<IOperatorAiEnableService, OperatorAiEnableService>();
builder.Services.AddHostedService<OperatorAiLiveBundleCacheStartupWarm>();

// ============================================================================
// APPLICATION CREATION AND HTTP PIPELINE CONFIGURATION
// ============================================================================

// Creates WebApplication instance - this instance represents our application
var app = builder.Build();

// Ensure wwwroot and uploads/avatars exist so UseStaticFiles can serve uploaded avatars
var webRoot = app.Environment.WebRootPath;
if (string.IsNullOrEmpty(webRoot))
    webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
try
{
    Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "avatars"));
    Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "stories"));
}
catch (Exception ex)
{
    Log.Warning(ex, "Could not create wwwroot/uploads/avatars directory");
}

// ============================================================================
// DATABASE INITIALIZATION
// ============================================================================

// Initialize database and create admin user if needed.
// Testing: no PostgreSQL migrate — use EnsureCreated + SeedDataOnlyAsync (see else branch) so `dotnet run` with ASPNETCORE_ENVIRONMENT=Testing works for manual/curl checks.
if (!app.Environment.IsEnvironment("Testing"))
{
    const int maxRetries = 5;
    const int delaySeconds = 3;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await DatabaseInitializer.InitializeAsync(app.Services);
            break;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database initialization attempt {Attempt}/{Max} failed", attempt, maxRetries);
            if (attempt == maxRetries)
            {
                Log.Warning("Database initialization failed after {Max} attempts, continuing anyway", maxRetries);
                break;
            }
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }
    }

    // Seed database with PageTypes, Faces, and Pages
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hostEnvironment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            await DatabaseSeeder.SeedAsync(
                context,
                ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(hostEnvironment, configuration));
            Log.Information("Database seeded successfully");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database seeding failed, continuing anyway");
    }

    // Seed users (2 admins + 30 regular users)
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await DatabaseSeeder.SeedUsersAsync(context, userManager);
            Log.Information("Users seeded successfully");
            await DatabaseSeeder.SeedFaceGridContentAsync(context);
            Log.Information("Face grid demo content seeded");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "User seeding failed, continuing anyway");
    }

    // Extend expired / Expired-state stories so GET /api/stories is not empty after 24h publish TTL (runs even if grid seed failed).
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseSeeder.ReactivateExpiredStoriesForStartupAsync(context);
            Log.Information("Story list reactivation (expired → extended) completed");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Story list reactivation on startup skipped");
    }

    // ============================================================================
    // AI SERVICE HEALTH CHECK
    // ============================================================================
    // Check AI service health via gRPC
    // This verifies that the AI Demo gRPC service is running and ready
    // The health check is performed after database initialization to ensure
    // all dependencies are available before the application starts serving requests
    try
    {
        using var aiScope = app.Services.CreateScope();
        var aiSettings = aiScope.ServiceProvider.GetRequiredService<IOperatorAiSystemSettingsProvider>();
        if (!await aiSettings.IsAiEnabledAsync())
        {
            Log.Information("AI service startup health check skipped — global AI support is disabled");
        }
        else
        {
            // Get AI service gRPC address from environment variable, configuration, or use default
            // Priority: Environment variable > Configuration > Default Docker service name
            var aiServiceAddress = Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS")
                ?? builder.Configuration["AiService:GrpcAddress"]
                ?? "http://ai-demo-dev:50051"; // Default Docker service name for development

            // Perform health check with 10 second timeout
            // This attempts to connect to the gRPC server and verify it's listening
            var isHealthy = await CheckAiServiceHealth.CheckHealthAsync(aiServiceAddress, timeoutSeconds: 10);

            if (isHealthy)
            {
                Log.Information("AI service health check passed at {GrpcAddress}", aiServiceAddress);
            }
            else
            {
                // Log warning but don't fail application startup
                // AI service may be starting up or temporarily unavailable
                Log.Warning("AI service health check failed at {GrpcAddress}. Service may not be ready yet.", aiServiceAddress);
            }
        }
    }
    catch (Exception ex)
    {
        // Log warning but continue application startup
        // Application can still function without AI service, though some features may be unavailable
        Log.Warning(ex, "AI service health check failed, continuing anyway");
    }
}
else
{
    try
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedDataOnlyAsync(context);
        Log.Information("Testing environment: EnsureCreated + SeedDataOnlyAsync completed");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Testing environment: database seed failed");
    }
}

// ============================================================================
// HTTP REQUEST PIPELINE - MIDDLEWARE
// ============================================================================
// Middleware executes in the order it is added
// Each middleware can modify request or response, or terminate the pipeline

// Enable CORS - must be before UseHttpsRedirection and authentication
app.UseCors();

if (useTransportHardening)
    app.UseForwardedHeaders();

app.UseMiddleware<HubQueryTokenRedactionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Swagger / OpenAPI UI: Development by default; Production only if Swagger:EnableInProduction=true (discouraged).
var swaggerUiEnabled = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Swagger:EnableInProduction", false);
if (swaggerUiEnabled)
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// BSH3-T1: HTTPS redirect + HSTS in Production / Hardened (Development keeps plain HTTP for CORS preflight).
if (useTransportHardening)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Adds custom routing middleware - implements face-based multi-tenant routing
// This middleware executes before OAuth2Middleware and authentication
// Rewrites URLs like /acme-corp/api/users to /api/users?requestFaceID=123
app.UseMiddleware<RoutingMiddleware>();

// Endpoint matching must run *after* the face prefix is stripped; otherwise /public/api/... would 404.
app.UseRouting();

// Adds custom OAuth2 middleware - validates client credentials and request signatures
// This middleware executes after routing, before authentication
app.UseMiddleware<OAuth2Middleware>();

// Static files for wwwroot assets; /uploads/* is blocked — use signed /api/uploads/serve (BE-U3).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Context.Response.ContentLength = 0;
        }
    },
});

// Adds authentication middleware - extracts and validates JWT tokens from requests
app.UseAuthentication();

// After JWT: enforce private-face auth and query/scope consistency (see middleware XML docs).
app.UseMiddleware<FaceScopeEnforcementMiddleware>();

// Adds authorization middleware - checks user permissions
app.UseAuthorization();

// ACL A21 — after routing + authZ so policies see connection metadata
app.UseRateLimiter();

// ============================================================================
// ENDPOINT MAPPING
// ============================================================================

// Maps SignalR ChatHub to endpoint /hubs/chat
// This hub requires authentication ([Authorize] attribute)
// User must connect with valid JWT token: wss://localhost:8001/hubs/chat?access_token=<token>
app.MapHub<ChatHub>("/hubs/chat").RequireRateLimiting("signalr-negotiate");
app.MapHub<MessengerHub>("/hubs/messenger").RequireRateLimiting("signalr-negotiate");
app.MapHub<ChatRoomHub>("/hubs/chatroom").RequireRateLimiting("signalr-negotiate");
app.MapHub<VideoLoungeHub>("/hubs/video-lounge").RequireRateLimiting("signalr-negotiate");

// Maps all controllers - automatically finds all controllers and creates endpoints from them
// E.g., OAuth2Controller with [Route("api/oauth2")] creates endpoints like /api/oauth2/token, /api/oauth2/register
// Per-endpoint policies (oauth-token, oauth-register, localization-read, auth-login) use [EnableRateLimiting]; avoid a
// global MapControllers policy so it does not stack with those named policies (BSH3-A4 partial).
app.MapControllers();

// Runs the application - application starts listening on configured ports
try
{
    Log.Information("Starting BeDemo API application");
    app.Run();
}
catch (Exception ex)
{
    // Logs any unhandled exceptions during application startup
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    // Ensures all buffered logs are written before application exits
    Log.CloseAndFlush();
}
