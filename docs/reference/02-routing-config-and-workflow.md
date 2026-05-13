# Backend reference — routing, configuration, workflow

**Navigation:** [« Index](../DETAILED_README.md) · [Part 1](./01-features-running-and-api.md) · **Part 2 (this file)** · [Part 3](./03-testing-integration-and-troubleshooting.md) · [Part 4](./04-database-schema-diagram.md)

---

## Multi-Tenant Face-Based Routing

The API implements multi-tenant routing using **face-based URL prefixes**. This allows each tenant (organization) to be identified by a unique face prefix in the URL, automatically scoping requests to that tenant.

### How It Works

When a request comes in with a face prefix (e.g., `/acme-corp/api/users`), the `RoutingMiddleware`:

1. **Extracts the face prefix** from the URL path (e.g., `acme-corp`)
2. **Converts to kebab-case** if needed (e.g., `AcmeCorp` → `acme-corp`)
3. **Looks up the face** in the database by matching the prefix against the `Face.Index` field
4. **Rewrites the URL** from `/{face-prefix}/{path}` to `/api/{face-id}/{path}?requestFaceID={id}`
5. **Returns 403 Forbidden** if no matching face is found for a route that requires it

### URL Transformation Examples

```
# Input URL (from frontend)
/acme-corp/dashboard

# Transformed to internal API call
/api/acme-corp/dashboard?requestFaceID=123

# Or for API endpoints
/acme-corp/api/users

# Transformed to
/api/acme-corp/users?requestFaceID=123
```

### Public Paths (Bypass Face Routing)

Certain paths bypass face routing and are accessible without a face prefix:

- `/api/` - Direct API access (when not prefixed with face)
- `/swagger` - Swagger UI documentation
- `/swagger-ui` - Swagger UI alternative
- `/openapi` - OpenAPI specification
- `/hubs` - SignalR hubs

### Face Matching Logic

1. Face prefix is extracted from the first URL segment
2. The prefix is converted to kebab-case (e.g., "AcmeCorp" → "acme-corp")
3. Database lookup finds a `Face` entity where `Face.Index` matches the prefix
4. If found, the `Face.Id` is added as `requestFaceID` query parameter
5. URL is rewritten to include the face ID in the path

### Configuration

The middleware is registered in `Program.cs`:

```csharp
// Register services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IFaceService, FaceService>();

// Add middleware (before OAuth2Middleware)
app.UseMiddleware<RoutingMiddleware>();
```

### Implementation Details

- **Caching**: Face data is cached in memory for 5 minutes to reduce database queries
- **Service**: `IFaceService` provides face lookup functionality
- **Utilities**: `Routing.cs` contains helper methods for path checking and kebab-case conversion
- **Performance**: Face cache reduces database load for frequently accessed faces

### Testing

Face routing is tested in the test suite. The middleware:

- Correctly identifies face prefixes in URLs
- Rewrites URLs with face ID
- Returns 403 for invalid face prefixes
- Bypasses public paths correctly

## Configuration

### Environment Variables

The API uses the following environment variables (configured in `docker-compose.dev.yml`):

- `ASPNETCORE_ENVIRONMENT` - Environment name (Development, Production, etc.)
- `ASPNETCORE_URLS` - URLs to bind to (e.g., `http://0.0.0.0:8000`)
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `Serilog__WriteTo__1__Args__serverUrl` - Seq logging server URL

### Database Connection

The default connection string format:

```
Host=host.docker.internal;Port=54320;Database=bedemo;Username=bedemo_user;Password=bedemo_password
```

- **From Docker containers**: Use `host.docker.internal` as host
- **From localhost**: Use `localhost` as host
- **Database**: `bedemo`
- **Username**: `bedemo_user`
- **Password**: `bedemo_password`

### Logging

Logs are sent to:

1. **Console** (stdout) - Visible in Docker logs
2. **Seq** - Structured logging server at `http://seq:5341` (internal) or `http://localhost:5341` (host)

View logs:

```bash
# Docker logs
docker-compose -f docker-compose.dev.yml logs -f be-demo-api

# Seq UI
open http://localhost:5341
```

## Database Migrations

### Creating a Migration

```bash
cd BeDemo.Api
dotnet ef migrations add MigrationName
```

### Applying Migrations

```bash
# In Docker
docker-compose -f docker-compose.dev.yml exec be-demo-api dotnet ef database update

# Locally
cd BeDemo.Api
dotnet ef database update
```

### Removing Last Migration

```bash
cd BeDemo.Api
dotnet ef migrations remove
```

## Development Workflow

1. **Start database**: Ensure PostgreSQL is running (via `many_faces_database` or monorepo `./scripts/start-all-dev.sh`)

2. **Start Redis** (optional, for job queue): submodule `many_faces_redis` or monorepo `./scripts/start-all-dev.sh`

3. **Start backend**: Run `./scripts/start-dev.sh` or use monorepo `./scripts/start-all-dev.sh` to start all services

4. **Make code changes**: Edit code in `BeDemo.Api/`

5. **Test changes**:
   - API endpoints via Swagger UI
   - Unit tests: `dotnet test` in `BeDemo.Api.Tests/`

6. **View logs**: Check Docker logs or Seq UI

7. **Stop services**: Run `./scripts/stop-dev.sh` or monorepo `./scripts/stop-all-dev.sh`

