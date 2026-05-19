# Backend reference — testing, integration, troubleshooting

**Navigation:** [« Index](../DETAILED_README.md) · [Part 1](./01-features-running-and-api.md) · [Part 2](./02-routing-config-and-workflow.md) · **Part 3 (this file)** · [Part 4](./04-database-schema-diagram.md)

---

## Testing

Run unit tests (no PostgreSQL required; tests use an in-memory database):

```bash
yarn test
# or from repo root: cd many_faces_backend && yarn test
```

Tests cover:

- **FluentValidation** — per-schema `*ValidatorTests`, shared `Validation/Rules/*RulesTests`, generated `ValidatorSection4MatrixTests`, integration `ValidationProblemDetailsIntegrationTests`; run `dotnet test --filter "FullyQualifiedName~Validation"` and `./scripts/verify-validator-tests-parity.sh`. Guide: monorepo [`api-request-validation.md`](../../../docs/guides/api-request-validation.md).
- Authentication and authorization
- OAuth2 flows (including **email-code registration**: `RegistrationInviteEdgeCaseTests`, `OAuth2EdgeCaseTests`)
- Edge cases and security scenarios
- SignalR hubs
- Performance tests
- **Content moderation security** — `ContentModerationTests`, `ContentModerationAlertEvaluatorTests`, `ContentModerationSecurityEdgeTests` (red-team corpus in `BeDemo.Api.Tests/Fixtures/prompt_injection_corpus.txt`), `ContentModerationUnicodeSpoofingTests` (SHV2 **PI-6** — bidi, zero-width, homoglyph, mixed-script), `ContentModerationTrustBoundaryTests` (SHV2 **PI-9** — untrusted `ReviewContent` vs trusted operator AI / public stats JSON), `ContentModerationPayloadLogRedactionTests` (SHV2 **PI-7** — invalid queue payload must not leak user text into logs)

```bash
dotnet test --filter "Category=ModerationSecurity"
dotnet test --filter "FullyQualifiedName~ContentModeration"
```

```bash
dotnet test --filter "FullyQualifiedName~ContentModerationUnicodeSpoofing"
dotnet test --filter "FullyQualifiedName~ContentModerationTrustBoundary"
dotnet test --filter "FullyQualifiedName~ContentModerationPayloadLogRedaction"
```

- **Static localization API** — `LocalizationControllerTests` (200/404, face-prefix exempt path); `LocalizationRateLimit429Tests` (`localization-read` policy → **429** + `Retry-After` via `RateLimitedLocalizationWebApplicationFactory`, serial xUnit collection). Each test host gets a unique `Testing:RateLimitScopeId` so rate-limit counters are not shared across parallel `WebApplicationFactory` instances.
- **Portal golden subtree (§11.1)** — `LocalizationPortalGoldenTests` compares `resources.en.common` auth-flow paths (`pages.login`, `pages.register`, `routes.login|register|homepage`) against `BeDemo.Api.Tests/Fixtures/portal-auth-flow-golden.en.json`. Regenerate intentionally changed copy with `REGENERATE_LOCALIZATION_GOLDEN=1 dotnet test --filter RegeneratePortalAuthFlowGolden`.
- **Ambiguous `.resx` prefixes (§4.2)** — `ResxLocalizationKeyAmbiguityTests` fails when any Portal/Admin/Mobile culture defines both a leaf key and a longer dotted child (e.g. `pages.login` + `pages.login.title`). Logic: `ResourceJsonUnflattener.FindAmbiguousFlatKeys`.

```bash
dotnet test --filter "FullyQualifiedName~Localization"
```

```bash
dotnet test --filter "FullyQualifiedName~LocalizationPortalGolden"
```

```bash
dotnet test --filter "FullyQualifiedName~ResxLocalizationKeyAmbiguity"
```

From monorepo root (parity + golden + ambiguity in one command):

```bash
node scripts/verify-localization-key-parity.mjs
```

See monorepo **[static-localization-and-i18n.md](../../../docs/guides/static-localization-and-i18n.md)**.

**Registration invite tests** use `RegistrationInviteWebApplicationFactory` with a fake **`CapturingMailerWorkerClient`** (`Mail:Enabled=true`). Run:

```bash
dotnet test --filter "FullyQualifiedName~RegistrationInvite"
```

See monorepo **[email-code-registration.md](../../../docs/guides/email-code-registration.md)**.

## Integration with Root Project

This backend is part of the **`many_faces_main`** monorepo (`many_faces_backend/` submodule on GitHub: `many_faces_backend`) and integrates with:

- **Database**: **many_faces_database** (`many_faces_database/`)
- **Redis**: **many_faces_redis** (`many_faces_redis/`)
- **Frontend**: **many_faces_portal** (`many_faces_portal/`)
- **Admin**: **many_faces_admin** (`many_faces_admin/`)
- **Many Faces AI service**: **many_faces_ai** (`many_faces_ai/`)
- **Many Faces log viewer**: **many_faces_logger** (`many_faces_logger/`)

From the **many_faces_main** repository root, use the orchestration scripts to manage all services:

- `./scripts/start-all-dev.sh` - Start all services
- `./scripts/stop-all-dev.sh` - Stop all services
- `./scripts/clear-all-dev.sh` - Clear all containers and volumes
- `./scripts/status-all.sh` - Show status of all services
- `./scripts/rebuild-all-dev.sh` - Rebuild all Docker images

## Troubleshooting

### Port Already Allocated

If port 8000 or 8001 is already in use:

```bash
# Find process using port
lsof -ti:8000,8001

# Kill process
lsof -ti:8000,8001 | xargs kill -9

# Or use clear script
./scripts/clear-dev.sh
```

### Database Connection Failed

- Ensure PostgreSQL container is running: `docker ps | grep postgres-dev`
- Check connection string in `docker-compose.dev.yml`
- Verify database credentials match `many_faces_database` configuration

### Seq Logging Server Not Accessible

- Ensure `seq-dev` container is running: `docker ps | grep seq-dev`
- Check Seq UI at `http://localhost:5341`
- Default credentials: `admin` / `admin`

### Migration Errors

- Ensure database is running and accessible
- Check connection string configuration
- Try removing and recreating migrations if needed
