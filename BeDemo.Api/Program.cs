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

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BeDemo.Api.Data;
using BeDemo.Api.Models;
using BeDemo.Api.Middlewares;
using BeDemo.Api.Services;
using BeDemo.Api.Hubs;
using BeDemo.Api.Scripts;
using Serilog;
using Grpc.Net.Client;

// Creates WebApplicationBuilder, which is used to configure the application
var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddControllers();

// Adds support for OpenAPI/Swagger - automatic API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================================
// CORS CONFIGURATION
// ============================================================================

// Configure CORS to allow frontend to access API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:8081",
                "http://localhost:8082",
                "http://localhost:8080",
                "https://localhost:8081",
                "https://localhost:8082",
                "https://localhost:8080"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// ============================================================================
// DATABASE CONFIGURATION (Entity Framework Core)
// ============================================================================

// Loads connection string from appsettings.json or environment variables
// Throws exception if connection string doesn't exist
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Log connection string for debugging (without password)
var connectionStringForLogging = connectionString.Contains("Password=") 
    ? connectionString.Substring(0, connectionString.IndexOf("Password=")) + "Password=***" 
    : connectionString;
Log.Information("Using connection string: {ConnectionString}", connectionStringForLogging);

// Configures Entity Framework Core to use PostgreSQL database
// PostgreSQL is a robust, open-source relational database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ============================================================================
// ASP.NET CORE IDENTITY CONFIGURATION
// ============================================================================

// Adds ASP.NET Core Identity framework for user, role and authentication management
// Identity automatically creates necessary database tables (Users, Roles, UserRoles, etc.)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password validation settings
    options.Password.RequireDigit = true;              // Password must contain at least one digit
    options.Password.RequireLowercase = true;           // Password must contain at least one lowercase letter
    options.Password.RequireUppercase = true;           // Password must contain at least one uppercase letter
    options.Password.RequireNonAlphanumeric = true;    // Password must contain at least one special character
    options.Password.RequiredLength = 4;                // Minimum password length is 4 characters
    
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

// Creates singleton instance of ECDSAKeyService - service for generating and managing ECDSA keys
// Singleton means only one instance is created for the entire application
// ECDSA (Elliptic Curve Digital Signature Algorithm) uses P-521 curve (521-bit key) - very strong encryption
var ecdsaKeyService = new ECDSAKeyService();
builder.Services.AddSingleton<IECDSAKeyService>(ecdsaKeyService);

// Adds OAuth2Service as scoped service - new instance for each HTTP request
// Scoped means the service lives during the HTTP request lifetime
builder.Services.AddScoped<IOAuth2Service, OAuth2Service>();

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
        ValidateIssuerSigningKey = true,                // Validates that token was signed with correct key
        IssuerSigningKey = signingKey,                 // Key used to validate signature
        ValidateIssuer = true,                         // Validates that token issuer is correct
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "BeDemoApi",  // Expected issuer
        ValidateAudience = true,                       // Validates that token audience is correct
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "BeDemoApi",  // Expected audience
        ValidateLifetime = false,                     // Disabled: don't validate token expiration (tokens never expire)
        ClockSkew = TimeSpan.Zero                     // No tolerance for time skew (precise time validation)
    };

    // Special configuration for SignalR WebSocket connections
    // SignalR uses WebSocket protocol which doesn't support HTTP headers, so token is sent via query parameter
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Gets access_token from query string (e.g., /hubs/chat?access_token=xxx)
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            // If request is to SignalR hub endpoint and contains access_token, use it for authentication
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
    };
});

// Adds authorization system - checks if user has permission to access resources
builder.Services.AddAuthorization();

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
// APPLICATION CREATION AND HTTP PIPELINE CONFIGURATION
// ============================================================================

// Creates WebApplication instance - this instance represents our application
var app = builder.Build();

// ============================================================================
// DATABASE INITIALIZATION
// ============================================================================

// Initialize database and create admin user if needed
// Skip in test environment (in-memory database doesn't support MigrateAsync)
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        await DatabaseInitializer.InitializeAsync(app.Services);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database initialization failed, continuing anyway");
    }

    // Seed database with PageTypes, Faces, and Pages
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseSeeder.SeedAsync(context);
            Log.Information("Database seeded successfully");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database seeding failed, continuing anyway");
    }

    // Check AI service health via gRPC
    // This verifies that the AI Demo gRPC service is running and ready
    try
    {
        var aiServiceAddress = Environment.GetEnvironmentVariable("AI_SERVICE_GRPC_ADDRESS") 
            ?? builder.Configuration["AiService:GrpcAddress"] 
            ?? "http://ai-demo-dev:50051";
        
        var isHealthy = await CheckAiServiceHealth.CheckHealthAsync(aiServiceAddress, timeoutSeconds: 10);
        
        if (isHealthy)
        {
            Log.Information("AI service health check passed at {GrpcAddress}", aiServiceAddress);
        }
        else
        {
            Log.Warning("AI service health check failed at {GrpcAddress}. Service may not be ready yet.", aiServiceAddress);
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "AI service health check failed, continuing anyway");
    }
}

// ============================================================================
// HTTP REQUEST PIPELINE - MIDDLEWARE
// ============================================================================
// Middleware executes in the order it is added
// Each middleware can modify request or response, or terminate the pipeline

// Enable CORS - must be before UseHttpsRedirection and authentication
app.UseCors();

// In development environment displays OpenAPI/Swagger documentation
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();      // Maps OpenAPI endpoint
    app.UseSwagger();      // Adds Swagger middleware
    app.UseSwaggerUI();    // Adds Swagger UI (web interface for testing API)
}

// Redirects all HTTP requests to HTTPS (if available)
// Skip HTTPS redirect in development to avoid CORS issues with preflight requests
// HTTPS redirect causes 307 which breaks CORS preflight requests
// Disabled HTTPS redirect completely for development
// if (!app.Environment.IsDevelopment())
// {
//     app.UseHttpsRedirection();
// }

// Adds custom OAuth2 middleware - validates client credentials and request signatures
// This middleware executes before authentication
app.UseMiddleware<OAuth2Middleware>();

// Adds authentication middleware - extracts and validates JWT tokens from requests
app.UseAuthentication();

// Adds authorization middleware - checks user permissions
app.UseAuthorization();

// ============================================================================
// ENDPOINT MAPPING
// ============================================================================

// Maps SignalR ChatHub to endpoint /hubs/chat
// This hub requires authentication ([Authorize] attribute)
// User must connect with valid JWT token: wss://localhost:8001/hubs/chat?access_token=<token>
app.MapHub<ChatHub>("/hubs/chat");

// Maps all controllers - automatically finds all controllers and creates endpoints from them
// E.g., OAuth2Controller with [Route("api/oauth2")] creates endpoints like /api/oauth2/token, /api/oauth2/register
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
