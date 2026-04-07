# Face profiles, visibility, and social data

This document describes the behaviour implemented in the API for per-face user profiles, directory visibility, visits, roles, and face-scoped social features (likes, comments, reviews).

## Concepts

### `UserFaceProfile`

- One row per `(UserProfile, Face)` when the user has interacted with that face (registration, visit, role assignment, etc.).
- **`Visited`**: set to `true` when the user switches into the face (see `POST /api/faces/{id}/visit`). Replaces any previous client-only “first visit” tracking.
- **`FaceRoleIntroCompleted`**: onboarding for private faces; the frontend uses it with `isPublic` to decide whether to show the face-role panel.
- **`IsActive`**: `true` when the user has a **non-host** face role in that face; hosts are considered inactive for directory purposes.
- **`DisplayName`**, **`AvatarUrl`**: face-specific presentation; APIs prefer these over global `UserProfile` when present.

### `Face`

- **`IsPublic`**: whether the face is reachable without authentication (unchanged from earlier behaviour).
- **`Visibility`**: enum for **profile directory / profile read** rules — `Public`, `Private`, `Face`, `Hidden` (JSON uses string enum names). Fine-grained authorization for who may **read** a profile by visibility is planned; the field is stored and exposed on admin/update APIs.
- **`AllowRecensions`**: when `false`, review endpoints return **no** reviews (existing rows stay in DB but are not returned).

### Social entities (scoped to a face profile)

- **Likes**: `UserFaceProfileLike` — one row per `(liker user, target UserFaceProfile)`; no aggregate count column.
- **Comments**: `UserFaceProfileComment` — flat thread per target profile (shared wall for that profile).
- **Reviews**: `UserFaceProfileReview` — `Title`, `Text`, `Stars` (1–6), one review per `(author, target profile)` (upsert on write). Hidden from API when `AllowRecensions` is false.

### Directory list

`GET /api/faces/{faceId}/profiles` returns users who have a **non-host** role in that face (hosts, including yourself as host, do not appear). Pagination query params: `page`, `pageSize`.

### Profile detail URL (frontend)

`/{faceIndex}/profile/{userId}` (with language prefix as in the rest of the app), e.g. `/en/basic/profile/{guid}`.

## HTTP API (summary)

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/faces/{id}/visit` | Auth: set `Visited = true` for current user on that face. |
| `POST` | `/api/faces/{id}/exit-face` | Auth: only if current role is **not** `FACE_HOST`. Deletes face-scoped likes/comments/reviews involving the user in that face, clears face profile fields, sets role to `FACE_HOST`, `IsActive = false`, keeps `Visited` / `FaceRoleIntroCompleted` as updated server-side. |
| `GET` | `/api/faces/{faceId}/profiles` | List directory (non-host only). |
| `GET` | `/api/faces/{faceId}/profiles/{userId}` | Profile detail (avatar/display rules in controller). |
| `POST` / `DELETE` | `.../profiles/{userId}/like` | Like / unlike (auth). |
| `GET` / `POST` | `.../profiles/{userId}/comments` | List / add comments. |
| `GET` / `POST` | `.../profiles/{userId}/reviews` | List (empty if recensions off) / upsert review. |

Face configuration for the current user (`myVisited`, `myFaceRoleIntroCompleted`, `visibility`, `allowRecensions`, etc.) is included in **`GET /api/faces/config`** (see `FacesController`).

Admin face updates (including `Visibility`, `AllowRecensions`) use the existing face admin/update flow (`FacesController` / DTOs).

## Tests

Integration-style tests live in `BeDemo.Api.Tests/FaceProfilesControllerTests.cs` (visit, list, exit, likes, comments, reviews when disabled, etc.). The test assembly disables xUnit parallelization so a single shared in-memory database remains deterministic.

## Quick `curl` checks (with bearer token)

Replace `BASE`, `TOKEN`, `FACE_ID`, `USER_ID` as needed.

```bash
# Mark visited (after login — obtain TOKEN from your OAuth flow)
curl -sS -X POST "$BASE/api/faces/$FACE_ID/visit" \
  -H "Authorization: Bearer $TOKEN"

# List profiles in face
curl -sS "$BASE/api/faces/$FACE_ID/profiles?page=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN"

# Profile detail
curl -sS "$BASE/api/faces/$FACE_ID/profiles/$USER_ID" \
  -H "Authorization: Bearer $TOKEN"

# Exit face (non-host only)
curl -sS -X POST "$BASE/api/faces/$FACE_ID/exit-face" \
  -H "Authorization: Bearer $TOKEN"
```

## Not implemented yet (by design)

- Full **authorization matrix** for `GET` profile by `Face.Visibility` (`Private` / `Face` / `Hidden` and admin overrides).
- **PATCH face** from a dedicated public contract beyond current admin endpoints.
- **Filters** on profile list beyond pagination.

These are reserved for a follow-up once the baseline flows are stable.
