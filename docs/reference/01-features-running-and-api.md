# Features, running the API, and HTTP surface

## Running locally

| Mode         | Command / doc                                                                                                        |
| ------------ | -------------------------------------------------------------------------------------------------------------------- |
| Full stack   | `ENABLE_*=1 ./scripts/start-all-dev.sh` from monorepo root — [`development.md`](../../../docs/guides/development.md) |
| Backend only | `many_faces_backend/README.md` — Docker `be-demo-dev` or `dotnet run`                                                |
| Ports        | API **8000/8001**; see [`dev-https.md`](../../../docs/guides/dev-https.md)                                           |

## OAuth2 and registration

- **Token:** `POST /api/oauth2/token` (password + refresh grants) — [`authentication-and-sessions.md`](../../../docs/guides/authentication-and-sessions.md)
- **Signup:** `register/request` + `register/complete` — [`email-code-registration.md`](../../../docs/guides/email-code-registration.md)
- Legacy `POST /api/oauth2/register` is **deprecated**.

## Request validation

FluentValidation on DTOs → **400** `ValidationProblemDetails` — [`api-request-validation.md`](../../../docs/guides/api-request-validation.md).

## Static localization

- `GET /api/localization/{app}` (`admin`, `portal`, …) — face-prefix **exempt** for bundle fetch.
- Source: `BeDemo.Api/Localization/**/AdminResources*.resx` etc. — [`static-localization-and-i18n.md`](../../../docs/guides/static-localization-and-i18n.md).

## Platform operator (`/admin/api/...`)

| Gate                 | Role                                 | Guide                                                                                     |
| -------------------- | ------------------------------------ | ----------------------------------------------------------------------------------------- |
| `CanManageAllFaces`  | **`SUPER_ADMIN`** + admin face scope | [`admin-superadmin-only-access.md`](../../../docs/guides/admin-superadmin-only-access.md) |
| `IsGlobalSuperAdmin` | **`SUPER_ADMIN`**                    | Moderation, operator-users, hard deletes                                                  |
| `IsGlobalAdmin`      | **`ADMIN` or `SUPER_ADMIN`**         | Tenant `GET …/faces/config` only                                                          |

Examples: `GET /api/Stats`, `GET/POST /api/users`, `GET/POST /api/faces`, `GET /api/admin/infra/worker-config`, operator AI routes — all require **`SUPER_ADMIN`** on admin face.

## Operator content routes

Face-detail inventory and moderation paths are documented per feature in monorepo guides (albums, blogs, reels, stories, chat rooms, profiles). Search Swagger tags **Operator**, **ContentModeration**, **AdminInfra**.

## Stories reference

Dedicated doc: [`../STORIES_API.md`](../../STORIES_API.md) (submodule root).

## OpenAPI

Regenerate FE clients: [`openapi-client-generation.md`](../../../docs/guides/openapi-client-generation.md).
