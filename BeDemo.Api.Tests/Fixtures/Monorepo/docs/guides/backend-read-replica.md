# Backend read replica routing (BE-RP30)

Document-only runbook for future PostgreSQL read replica adoption. **No replica wiring in v1.**

## Replica-safe (eventual consistency OK)

| Endpoint / flow                     | Notes                                 |
| ----------------------------------- | ------------------------------------- |
| `GET /api/localization/{app}`       | Static bundles; 304 by version hash   |
| `GET /{face}/api/faces/config`      | Short TTL cache; invalidate on writes |
| `GET /api/Stats/public`             | Anonymous aggregates; 60s cache       |
| Grid list endpoints + grid snapshot | Portal lists; tolerate slight lag     |
| `GET /api/uploads/serve`            | Static files; HMAC gate unchanged     |

## Primary-only (strong consistency required)

| Endpoint / flow                   | Why                           |
| --------------------------------- | ----------------------------- |
| `POST /api/oauth2/token`          | Auth grants, refresh rotation |
| JWT `OnTokenValidated` / `atv`    | Session revocation            |
| `GET /api/me/capabilities`        | Fresh role/ACL for UI gates   |
| `GET /api/messages/conversations` | Unread counts                 |
| All mutating controllers          | Writes, moderation, outbox    |
| Search outbox processor           | Must see committed rows       |

## Implementation seam (optional)

Introduce `IReadOnlyDbContext` registered against a read connection string when ops provisions a replica. Start with faces config + public stats only; keep capabilities and messenger on primary.
