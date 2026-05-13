# Many Faces API — detailed reference

This file continues the submodule [`README.md`](../README.md): long-form product and API notes split for easier navigation. **Architecture, security, and moderation** remain on the main README.

**Navigation:** **Part 1 (this file)** · [Part 2 — routing & config](./02-routing-config-and-workflow.md) · [Part 3 — testing & troubleshooting](./03-testing-integration-and-troubleshooting.md) · [Part 4 — ER diagram](./04-database-schema-diagram.md) · [« Index](../DETAILED_README.md)

---

## Features

- **User Authentication & Authorization**
  - OAuth2 token-based authentication
  - User registration and login
  - JWT token generation and validation
  - Refresh token support

- **Database Integration**
  - Entity Framework Core with PostgreSQL
  - Code-first migrations
  - Automatic database seeding (admin user creation)

- **Roles (global and face-scoped)**
  - **Global roles** (one per user): SUPER_ADMIN, ADMIN, USER, HOST — stored in `ApplicationUser.UserRoleId`.
  - **Face roles** (per user per face): FACE_ADMIN, FACE_USER, INZERENT, SUBSCRIBER, FACE_HOST — stored in `UserFaceRole` (UserId, FaceId, UserRoleId).
  - On **registration**: user gets global role **USER**; for each face they get **UserFaceRole** with **FACE_HOST**.
  - **First visit to a private face**: frontend can show a role selector; user chooses role and backend **PUT /api/faces/{id}/my-role** updates or creates `UserFaceRole`. Config endpoint returns **myFaceRoleId** / **myFaceRoleName** when called with Authorization.
  - See `UserRole.GlobalRoleNames`, `UserRole.FaceRoleNames`, and `RoleScope` (Global/Face).

- **Default pages when creating a face**
  - Creating a face (POST /api/faces) adds **Home** (`/home`). If the face is **non-public**, a **Wall** page (`/wall`) is added. **PageTypes** in CMS are only **`home`**, **`static`**, **`wall`** (e.g. login/register use `static` on the public face). Typed **list/detail** UIs are frontend routes, not CMS page types.

- **API Documentation**
  - Swagger/OpenAPI documentation
  - Interactive API explorer at `/swagger`

- **Structured Logging**
  - Serilog integration
  - Seq logging server for log viewing and analysis
  - Console and Seq sinks

- **SignalR Support**
  - Real-time communication via SignalR hubs
  - Chat hub implementation

- **Multi-Tenant Face-Based Routing**
  - URL-based tenant identification via face prefix (e.g., `/acme-corp/dashboard`)
  - Automatic URL rewriting from `/{face-prefix}/{path}` to `/api/{face-id}/{path}?requestFaceID={id}`
  - Face prefix matching using kebab-case conversion (e.g., "AcmeCorp" → "acme-corp")
  - In-memory caching for face data (5-minute TTL)
  - Public paths bypass face routing (`/api/`, `/swagger`, `/hubs`, etc.)
  - Face validation and 403 Forbidden response for invalid face prefixes

## Technologies

- **.NET 10.0** - Latest .NET runtime
- **ASP.NET Core WebAPI** - RESTful API framework
- **Entity Framework Core 10.0** - ORM for database access
- **ASP.NET Core Identity** - User authentication and authorization
- **PostgreSQL** - Relational database
- **Serilog** - Structured logging
- **Seq** - Log viewing and analysis server
- **Swagger/OpenAPI** - API documentation

## Project Structure

```
many_faces_backend/
├── BeDemo.Api/              # Main API project
│   ├── Controllers/         # API controllers (Auth, OAuth2, Users, Faces, Pages)
│   ├── Services/            # Business logic services (IFaceService, FaceService)
│   ├── Models/              # Data models and DTOs
│   ├── Data/                # DbContext and data access
│   ├── Middlewares/         # Custom middleware (OAuth2, RoutingMiddleware)
│   ├── Utils/               # Utility classes (Routing helpers)
│   ├── Hubs/                # SignalR hubs
│   ├── Scripts/             # Initialization and health check scripts
│   └── Migrations/          # Database migrations
├── BeDemo.Api.Tests/        # Unit tests
├── docker-compose.dev.yml   # Docker Compose configuration for development
├── scripts/                 # Shell helpers (start/stop/clear/rebuild dev, lint, generate-diagram)
└── README.md                # Main submodule README (`../README.md`)
```

## Running

### Running in Docker Container (Recommended)

The easiest way to run the backend API in development:

```bash
./scripts/start-dev.sh
```

This script will:

1. Stop any existing containers
2. Create HTTPS certificate if needed
3. Build Docker images
4. Start containers (backend API and Seq logging server)
5. Run database migrations
6. Wait for services to be ready

The API will be available at:

- **HTTP**: `http://localhost:8000`
- **HTTPS**: `https://localhost:8001`
- **Swagger UI**: `http://localhost:8000/swagger`
- **Seq Logging UI**: `http://localhost:5341`

### Manual Docker Compose

```bash
docker-compose -f docker-compose.dev.yml up --build
```

### Stopping Services

```bash
./scripts/stop-dev.sh
```

Or manually:

```bash
docker-compose -f docker-compose.dev.yml down
```

### Clearing Everything

```bash
./scripts/clear-dev.sh
```

This removes containers, volumes, and images.

### Rebuilding Docker Images

To perform a clean rebuild of Docker images:

```bash
./scripts/rebuild-dev.sh
```

**Note**: This only builds images, it does NOT start containers. Use `./scripts/start-dev.sh` to start containers after rebuilding.

### Local Development (Without Docker)

1. **Ensure PostgreSQL is running** (see `many_faces_database` folder or monorepo `./scripts/start-all-dev.sh`)
2. **For job queue**: Redis via submodule `many_faces_redis` (`./scripts/start-redis.sh` from that repo, or monorepo `./scripts/start-all-dev.sh`)

3. **Install .NET SDK 10.0**

4. **Restore dependencies**:

   ```bash
   cd BeDemo.Api
   dotnet restore
   ```

5. **Update database**:

   ```bash
   dotnet ef database update
   ```

6. **Run the application**:

   ```bash
   dotnet run --launch-profile http
   ```

   Or with HTTPS:

   ```bash
   dotnet run --launch-profile https
   ```

7. **Access Swagger UI**: `http://localhost:8000/swagger`

## API Endpoints

### Authentication

- `POST /api/auth/register` - Register a new user
  - Request body: `RegisterModel` (email, password, firstName, lastName)
  - Returns: User information

- `POST /api/auth/login` - Login with email and password
  - Request body: `LoginModel` (email, password)
  - Returns: User information and authentication token

- `POST /api/auth/logout` - Logout current user
  - Requires: Authentication header

### OAuth2

- `POST /api/oauth2/token` - Get OAuth2 access token
  - Request body: `OAuth2TokenRequest` (`grantType`, `username`, `password`, `clientId`, `clientSecret`, optional **`rememberMe`**)
  - **`rememberMe: true`** (password grant) selects **`Jwt:ExpiresInMinutesRememberMe`** for access-token lifetime; omitted/false uses **`Jwt:ExpiresInMinutes`**. See monorepo [**authentication-and-sessions**](../../docs/guides/authentication-and-sessions.md).
  - Returns: `OAuth2TokenResponse` with `accessToken`, `refreshToken`, `expiresIn` (seconds), `tokenType`
  - **Refresh grant:** `grant_type=refresh_token` rotates stored refresh tokens (see `OAuthRefreshTokenStore`); details in monorepo [**acl-and-capabilities**](../../docs/guides/acl-and-capabilities.md).

- `POST /api/oauth2/register` - Register new user via OAuth2 flow
  - Request body: `OAuth2RegisterModel` (email, password, firstName, lastName)

### Users

- `GET /api/users` - Get all users (admin only)
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

### Faces

- `GET /api/faces` - Get all faces
- `GET /api/faces/config` - Get all faces with pages (for routing). When request includes Authorization, each face includes **myFaceRoleId** and **myFaceRoleName** for the current user.
- `GET /api/faces/face-roles` - Get list of face-scoped roles `[{ id, name }]` (for role selector on first visit to a private face).
- `GET /api/faces/{id}` - Get face by ID
- `PUT /api/faces/{id}/my-role` - Set current user's face role for this face. Body: `{ userRoleId }`. Creates or updates UserFaceRole.
- `POST /api/faces` - Create new face
- `PUT /api/faces/{id}` - Update face
- `DELETE /api/faces/{id}` - Delete face

### Pages

- `GET /api/pages` - Get all pages
- `GET /api/pages/{id}` - Get page by ID
- `POST /api/pages` - Create new page
- `PUT /api/pages/{id}` - Update page
- `DELETE /api/pages/{id}` - Delete page

### Page Types

- `GET /api/pagetypes` - Get all page types
- `GET /api/pagetypes/{id}` - Get page type by ID
- `POST /api/pagetypes` - Create new page type
- `PUT /api/pagetypes/{id}` - Update page type
- `DELETE /api/pagetypes/{id}` - Delete page type

For detailed API documentation, visit the Swagger UI at `http://localhost:8000/swagger` when the API is running.

