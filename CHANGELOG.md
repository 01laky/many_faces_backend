# Changelog

All notable changes to **`many_faces_backend`** are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) — **version headings only, no dates**. SemVer: [`VERSION`](./VERSION).

### Release index

| Version       | Theme                                              |
| ------------- | -------------------------------------------------- |
| [1.0.0](#100) | Operator infra settings, global search, live stats |
| [0.9.0](#090) | Operator consoles, VideoLounge, moderation APIs    |
| [0.8.0](#080) | SHV2 security, FluentValidation, signed uploads    |
| [0.7.0](#070) | Email-code registration, static i18n bundles       |
| [0.6.0](#060) | Workers gRPC, content moderation, many_faces_proto |
| [0.5.0](#050) | ACL, refresh tokens, remember-me, demo seeds       |
| [0.4.0](#040) | Social modules, grid, stories, chat, wall          |
| [0.3.0](#030) | Face-based multi-tenant routing                    |
| [0.2.0](#020) | UserProfile, RBAC, integration tests               |
| [0.1.0](#010) | .NET API foundation, PostgreSQL, Docker dev        |

## [Unreleased]

### Added

### Changed

### Fixed

---

## [1.0.1]

### Changed

- Document project author (Ladislav Kostolny, 01laky@gmail.com) in README and standard manifests.

### Added

### Changed

- Document project author (Ladislav Kostolny, 01laky@gmail.com) in README and standard manifests.

### Fixed

---

## [1.0.0]

### Added

- Operator push settings API and FCM test endpoint; operator mail settings (DB SMTP).
- Admin global search autocomplete, outbox indexing, reconciliation job.
- Operator AI live stats map-reduce; Redis cache stage 1; AI worker host profile API.
- Global AI master switch; de/fr/it admin localization bundles; super-admin self profile API.

### Changed

- BSH3 backend hardening; Phase A DRY utilities; platform operator ACL super-admin only.

### Fixed

- Mail test double for shared in-memory DB; operator story detail bypasses portal live-only filter.

## [0.9.0]

### Added

- Operator AI chat (PostgreSQL threads, locale-aware); operator user detail and platform DMs.
- Operator content management (albums, reels, blogs, stories, face profile, chat rooms).
- VideoLounge module with demo seeds; server-driven admin list pagination/sort/filter.
- Operator wall-ticket create and staff comments; profile detail template pages.

### Changed

- Removed operator registration invite admin APIs.

## [0.8.0]

### Added

- SHV2 moderation PI-1–PI-10; FluentValidation rollout; upload limits and HMAC signed URLs.
- Remember-me cap at 7 days; password minimum 12 characters.

### Fixed

- Invalid AI review payload log redaction; admin face protected from cross-face mutations.

## [0.7.0]

### Added

- Email-code registration with invites; static UI strings from `.resx` localization bundles.

### Fixed

- CORS for localhost 9080/9081; PII redaction in OAuth and registration logs.

## [0.6.0]

### Added

- User content moderation and AI review jobs; gRPC clients for elastic, push, mailer workers.
- Nested `many_faces_proto` submodule; device tokens; Identity mail via mailer worker.

### Changed

- Split OAuth2 token flow into dedicated services.

## [0.5.0]

### Added

- ACL capabilities, refresh token rotation, rate limits; face grid demo seeding; remember-me JWT.

### Fixed

- Story upload DTO mapping; gRPC channel config on ARM64 Docker.

## [0.4.0]

### Added

- Grid schema, albums, blog, reels, face profiles, stories, chat rooms, face wall tickets.

### Changed

- Page types reduced to home, static, wall (dropped CMS list type).

## [0.3.0]

### Added

- Face-based routing middleware; global vs face-scoped roles; StatsController; Friend/Messenger APIs.

## [0.2.0]

### Added

- UserProfile entity, UserRole RBAC, controller integration tests, auto-generated ERD diagrams.

## [0.1.0]

### Added

- .NET WebAPI foundation with Identity, PostgreSQL, OAuth2/JWT, Docker compose, gRPC AI health probe.

[Unreleased]: https://github.com/01laky/many_faces_backend/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/01laky/many_faces_backend/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/01laky/many_faces_backend/compare/v0.9.0...v1.0.0
[0.9.0]: https://github.com/01laky/many_faces_backend/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/01laky/many_faces_backend/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/01laky/many_faces_backend/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/01laky/many_faces_backend/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/01laky/many_faces_backend/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/01laky/many_faces_backend/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/01laky/many_faces_backend/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/01laky/many_faces_backend/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/01laky/many_faces_backend/releases/tag/v0.1.0
