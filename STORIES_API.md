# Stories API

Stories are **draft → scheduled (optional) → published (24h) → expired**. Interactions (likes, comments, views) are removed on expiry; the creator can **publish again** from the same row. Each creator keeps at most **three** story rows (oldest deleted when a fourth would exist).

**Lists** (`GET /api/stories`) are only returned for viewers who have a **non-host** face role in the given face. **No `faceIds` on create** means the story targets **all** faces (same idea as reels).

**Full OAuth2 + face role + curl walkthrough** (Slovak, step-by-step): in the **`many_faces_main`** monorepo root, see [`docs/api-oauth-stories-curl.md`](../docs/api-oauth-stories-curl.md).

## Endpoints

| Method            | Path                                 | Notes                                                        |
| ----------------- | ------------------------------------ | ------------------------------------------------------------ |
| `GET`             | `/api/stories?faceId=`               | Live published stories for this face (non-host only).        |
| `GET`             | `/api/stories/me`                    | Creator’s stories; optional `faceId` filter.                 |
| `GET`             | `/api/stories/{id}?faceId=`          | Detail; creator sees `viewers`, others see `viewCount` only. |
| `POST`            | `/api/stories`                       | Body: `{ "title", "faceIds": [] optional }`.                 |
| `POST`            | `/api/stories/{id}/images`           | Multipart: `file`, `sortOrder` 0–9, optional `description`.  |
| `POST`            | `/api/stories/{id}/publish`          | Body: `{ "scheduledPublishAt": null or ISO UTC }`.           |
| `DELETE`          | `/api/stories/{id}`                  | Creator only.                                                |
| `POST`            | `/api/stories/{id}/view?faceId=`     | Idempotent view record.                                      |
| `GET/POST/DELETE` | `/api/stories/{id}/likes?faceId=`    | Like / unlike (live story, non-host).                        |
| `GET/POST`        | `/api/stories/{id}/comments?faceId=` | Body: `{ "content" }`.                                       |

Redis jobs: `story.publish` (scheduled), `story.expire` (24h after publish).

## curl example

```bash
# Docker dev: 8000. dotnet run (launchSettings): often 8080.
BASE=http://127.0.0.1:8000
# 1) Register + OAuth2 token — see many_faces_main/docs/api-oauth-stories-curl.md
# 2) PUT /api/faces/{faceId}/my-role with non-host role (e.g. FACE_USER)
# 3) Create draft
STORY_ID=$(curl -sS -X POST "$BASE/api/stories" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Hello"}' | jq -r .id)

# 4) Upload image (jpeg bytes)
curl -sS -X POST "$BASE/api/stories/$STORY_ID/images" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/path/to/photo.jpg" -F "sortOrder=0" -F "description=caption"

# 5) Publish now
curl -sS -X POST "$BASE/api/stories/$STORY_ID/publish" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"scheduledPublishAt":null}'

# 6) List
curl -sS "$BASE/api/stories?faceId=$FACE_ID" -H "Authorization: Bearer $TOKEN"
```
