# Stories HTTP API (reference)

This document complements **[`docs/guides/api-oauth-stories-curl.md`](../docs/guides/api-oauth-stories-curl.md)** (step-by-step **curl** smoke). For **authoritative route shapes**, use **Swagger** (`/swagger`) or **`/swagger/v1/swagger.json`** while the API is running.

**Maintenance:** whenever Stories controllers or DTOs change, refresh this table and the curl guide in the **same PR** as OpenAPI / SPA client regeneration (**[`openapi-client-generation.md`](../docs/guides/openapi-client-generation.md)**) so the three sources (Swagger, this file, curl doc) stay aligned.

## Base URL and face prefix

- Local smoke often uses `http://127.0.0.1:8000` as `BASE`.
- In multi-face deployments, callers typically use a **face-prefixed** path such as `/{facePrefix}/api/...` (see **`RoutingMiddleware`** and [`docs/guides/authentication-and-sessions.md`](../docs/guides/authentication-and-sessions.md)).

Unless noted, endpoints below assume **`$BASE/api/stories`** as in the curl guide (adjust for your prefix).

## Endpoints (overview)

| Method | Path | Auth | Notes |
| ------ | ---- | ---- | ----- |
| `POST` | `/api/stories` | Bearer | Create **draft**. Body may include `title`, optional `faceIds` (empty = all faces). |
| `POST` | `/api/stories/{id}/images` | Bearer | **Multipart** upload; at least one image before publish. Fields: `file`, `sortOrder` (0–9), optional `description`. |
| `POST` | `/api/stories/{id}/publish` | Bearer | Publish (immediate or scheduled). Body: `scheduledPublishAt` (ISO UTC or `null`). Worker may process job `story.publish` when scheduled. |
| `GET` | `/api/stories?faceId={faceId}` | Bearer | List stories for a **face** (viewer / host rules apply). |
| `GET` | `/api/stories/{id}?faceId=…` | Bearer | Story **detail** for a face context. |
| `GET` | `/api/stories/me` | Bearer | Current user’s stories. |
| `POST` | `/api/stories/{id}/view?faceId=…` | Bearer | Record a **view** (analytics). |

## Likes and comments

Exact paths and DTOs are defined in **OpenAPI** / controllers (search `StoryLikes`, `StoryComments` in the backend). Regenerate SPA clients after contract changes (**[`openapi-client-generation.md`](../docs/guides/openapi-client-generation.md)**).

## Related

- [`api-oauth-stories-curl.md`](../docs/guides/api-oauth-stories-curl.md) — full curl script and OAuth prelude.
- [`docs/DETAILED_README.md`](./docs/DETAILED_README.md) — index to the split **detailed reference** (`docs/reference/*.md`: non-Stories endpoint bullets, routing, ER diagram).
- Monorepo **database / Redis** guides if you debug publish workers or list visibility.
