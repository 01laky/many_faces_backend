# Changelog

All notable changes to **`many_faces_backend`** are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) — **version headings only, no dates**. SemVer: [`VERSION`](./VERSION).

### Release index

| Version        | Theme                                              |
| -------------- | -------------------------------------------------- |
| [1.4.12](#1412) | Backend refactor X5/X6 auth-policy migration (4)  |
| [1.4.11](#1411) | Backend refactor X5/X6 auth-policy migration (3)  |
| [1.4.10](#1410) | Backend refactor X5/X6 auth-policy migration (2)  |
| [1.4.9](#149)  | Backend refactor X5/X6 auth-policy migration (1)   |
| [1.4.8](#148) | Backend refactor X4 ProblemDetails errors (flag)   |
| [1.4.7](#147) | Backend refactor X14 health/readiness probes       |
| [1.4.6](#146) | Backend refactor X13 correlation-id middleware     |
| [1.4.5](#145) | Backend refactor X5 declarative auth policies      |
| [1.4.4](#144) | Backend refactor PII redaction + dead-code cleanup |
| [1.4.3](#143) | Backend refactor Phase 1 options validation        |
| [1.4.2](#142) | Backend refactor Phase 1 security hardening        |
| [1.4.1](#141) | Backend refactor Phase 0a (test safety nets)       |
| [1.4.0](#140) | Operator AI 7B performance (fast-paths, streaming) |
| [1.3.0](#130) | Operator AI skills (router + 4 skills)             |
| [1.2.0](#120) | Operator AI RAG retrieval (Elasticsearch kNN+BM25) |
| [1.1.0](#110) | Backend runtime performance v1 (BE-RP1…35)         |
| [1.0.2](#102) | README shield badges                               |
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

## [1.4.12]

### Changed

- **Backend refactor — authorization-policy migration, batch 4 (X5/X6).** Migrated the large `OperatorAiConversationsController` from a per-action `RequireOperator()` (= `CanManageAllFaces`) gate — repeated on all **15** endpoints (the operator AI inbox CRUD plus the `~/api/operator-ai/*` system-settings / model-status / worker-host / live-stats-cache / public-stats-settings surfaces) — to the declarative `ManageAllFaces` policy. The controller now carries `[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]`; all 15 `if (!RequireOperator()) return Forbid();` blocks, the `RequireOperator()` helper, and the now-unused `IAccessEvaluator` dependency were removed. Authorization matrix unchanged (anonymous → 401, global ADMIN → 403, SUPER_ADMIN in admin scope → allowed); the controller's integration suite, `OperatorAiSystemSettingsIntegrationTests`, and `PlatformSuperAdminAccessEdge` (incl. `OperatorAiConversations_ReturnsForbidden_ForGlobalAdmin`) stay green.

---

## [1.4.11]

### Changed

- **Backend refactor — authorization-policy migration, batch 3 (X5/X6).** Migrated `OperatorAiKnowledgeController` (`POST /api/operator-ai/knowledge/reindex`, `GET /api/operator-ai/knowledge/status`) from its in-body `RequireOperator()` (= `CanManageAllFaces`) gate to the declarative `ManageAllFaces` policy: the controller now carries `[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]`, and the `RequireOperator()` helper + the now-unused `IAccessEvaluator` dependency were removed. Authorization matrix unchanged (anonymous → 401, global ADMIN → 403, SUPER_ADMIN in admin scope → allowed).
  - Because the controller's unit tests constructed it directly and asserted `ForbidResult` from the in-body gate, the two `*_denies_non_operator` unit tests were removed (the controller no longer gates in-body) and replaced by an integration negative `PlatformSuperAdminAccessEdgeTests.OperatorAiKnowledge_ReturnsForbidden_ForGlobalAdmin` that exercises the policy on both endpoints. The remaining unit tests (409/503/ok/skip behaviour) were updated to the slimmer constructor. Full suite stays green.

---

## [1.4.10]

### Changed

- **Backend refactor — authorization-policy migration, batch 2 (X5/X6).** Migrated three more blanket-gate operator controllers from the in-body `if (!_access.CanManageAllFaces(User)) return Forbid();` check to the declarative `ManageAllFaces` policy: `AdminPushSettingsController` (`/api/admin/push/settings*`, 3 actions), `AdminMailSettingsController` (`/api/admin/mail/settings*`, 3 actions) and `AdminMailerTestController` (`POST /api/admin/mailer/test-self`). Each now carries `[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]` at the class level and the per-action gate plus the now-unused `IAccessEvaluator` dependency were removed. `AdminMailerTestController`'s `GET /api/admin/mailer/pilot-link` keeps its `[AllowAnonymous]` (method-level wins over the class policy), so it stays publicly reachable as before. The policy reproduces `PlatformAccessRules.CanManageAllFaces` exactly, so each controller's authorization matrix is unchanged (anonymous → 401, global ADMIN → 403, SUPER_ADMIN in admin scope → allowed); the existing `AdminPushSettings` / `AdminMailSettings` / `AdminMailerTest` integration suites (incl. their `401WithoutJwt` + `403ForGlobalAdmin` negatives) and `PlatformSuperAdminAccessEdge` stay green.

---

## [1.4.9]

### Changed

- **Backend refactor — authorization-policy migration, batch 1 (X5/X6).** Migrated the two clean blanket-gate operator controllers from an in-body imperative gate to the declarative `ManageAllFaces` policy: `AdminInfraController` (`GET /api/admin/infra/worker-config`) and `AdminPushTestController` (`POST /api/admin/push/test-self`) now carry `[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]` and the `if (!_access.CanManageAllFaces(User)) return Forbid();` check (plus the now-unused `IAccessEvaluator` dependency) was removed. The policy reproduces `PlatformAccessRules.CanManageAllFaces` (admin face scope AND global SUPER_ADMIN) exactly, so the authorization matrix is unchanged: anonymous → 401, authenticated-but-insufficient (global ADMIN) → 403, SUPER_ADMIN on a non-admin face → 403, SUPER_ADMIN in admin scope → allowed. The secondary `NameIdentifier`-claim guard inside each action is kept (it protects a per-account query; it is not an authz gate). The existing `AdminInfraController` / `AdminPushTestController` / `PlatformSuperAdminAccessEdge` integration suites already pin the full matrix and stay green; a new `InfraWorkerConfig_ReturnsForbidden_ForSuperAdmin_OnPublicFace` test locks the scope edge. (Branching `CanManageAllFaces()` usages in Pages/Stories/Reels controllers are deliberately left — they drive per-item logic, not a blanket gate, so they are not attribute-replaceable.)

---

## [1.4.8]

### Added

- **Backend refactor — global ProblemDetails exception handler (X4, flag-gated).** New `Middlewares/ProblemDetailsExceptionHandler` (`IExceptionHandler`) turns an unhandled exception into a consistent RFC 7807 `application/problem+json` 500 (generic title + `traceId` set to the X13 correlation id + request path as `instance`) instead of a bare/blank 500. The exception is logged server-side with the correlation id; the response body never carries the exception detail except in the Development environment. Opt-in behind a new `ErrorHandling:UseProblemDetails` flag (**default `false`** in `appsettings.json`) — `AddExceptionHandler` / `AddProblemDetails` and the pipeline `UseExceptionHandler()` (placed right after the correlation-id middleware) are wired only when the flag is on, so existing error behaviour is unchanged until an operator enables it (prompt §10: ship cross-cutting changes behind a flag). Covered by 3 handler tests (problem+json 500 shape with traceId; Production does not leak the exception message/detail; Development includes it).

---

## [1.4.7]

### Added

- **Backend refactor — health/readiness probes (X14).** Two anonymous infrastructure endpoints for orchestrator/load-balancer probes: `GET /health/live` (liveness — 200 while the process can serve a request, no dependency checks) and `GET /health/ready` (readiness — runs the `ready`-tagged checks and returns 503 when a hard dependency is down). Backed by a new `HealthChecks/DatabaseReadinessHealthCheck` that pings PostgreSQL via `ApplicationDbContext.Database.CanConnectAsync` (resolved in a per-check DI scope) and degrades failures to `Unhealthy` rather than throwing. Registered via `AddHealthChecks().AddCheck<…>("database", tags: ["ready"])` and mapped with `.AllowAnonymous()` so the default-deny `FallbackPolicy` does not 401 the probes; `/health` is added to `Routing` face-scope exemptions so the probes answer without a face prefix. Responses are the bare status word only — no check/exception detail leaks to an anonymous caller. Covered by 8 tests (routing-exemption matrix, readiness reachable + throws→Unhealthy branches, and anonymous end-to-end liveness/readiness through the real pipeline).

---

## [1.4.6]

### Added

- **Backend refactor — correlation-id middleware (X13).** New `Middlewares/CorrelationIdMiddleware` assigns every request a stable correlation id, taken from a **safe** inbound `X-Correlation-Id` / `X-Request-Id` header (so a trace can be followed across gateway/frontend/worker hops) or freshly generated (`Guid` `N` format) when absent. The id is pushed into the logging scope as `CorrelationId` (every log line for the request carries it), mirrored back on the response `X-Correlation-Id` header, and copied into `HttpContext.TraceIdentifier` so framework diagnostics line up. Inbound values are validated against a conservative allow-list (non-empty, ≤ 128 chars, only `A–Z a–z 0–9 - _ .`) before being echoed or logged — an unsafe value (whitespace, CR/LF, control or other punctuation) is discarded and a server id generated instead, closing a log-injection / response-header-reflection vector. Registered early in the pipeline (after forwarded-headers, before the redaction/security-header middleware) so the scope wraps the whole request. Covered by 17 tests (allow-list matrix + end-to-end echo: generate-when-absent, honour-safe-inbound on both headers, discard-unsafe-inbound).

---

## [1.4.5]

### Added

- **Backend refactor — declarative authorization policies (X5).** New `Security/PlatformAuthorizationPolicies` registers three named policies that mirror the imperative `PlatformAccessRules` checks: `SuperAdmin` and `GlobalAdmin` (claims-based `RequireAssertion`), and `ManageAllFaces` (a `ManageAllFacesRequirement` + scoped `ManageAllFacesAuthorizationHandler` that injects the request-scoped `IFaceScopeContext` and reproduces `CanManageAllFaces` = admin face scope AND global super-admin). Wired into `Program.cs` `AddAuthorization` alongside the existing default-deny fallback. **Additive and behaviour-preserving** — no controller enforces the policies yet; the per-controller migration from in-body `Forbid()` checks to `[Authorize(Policy = …)]` follows incrementally (ADR 0001 / prompt §10.3: keep the imperative gate until each policy has a passing negative test, never delete a gate and add a policy in the same commit). Covered by 9 tests (handler scope/role matrix, policy-registration smoke, and `IAuthorizationService` parity for the claims policies).

---

## [1.4.4]

### Fixed

- **Backend refactor — PII redaction (§2).** `OAuth2Middleware` no longer logs the raw OAuth `client_id` on an invalid-client 401 — it is hashed via the shared `PiiLogRedaction.FormatCredentialIdentifierForLog` (length + SHA-256 prefix), consistent with the repo's PII-redaction house rule.
- **Backend refactor — dead code (§6).** Removed the unused `ChatHub.BuildHistoryPlainText` (zero callers). (`ChatHub.SendToAi` / `OperatorAiHubErrorCodes.InvalidLocale` are left pending — each has a contract/value test or a client-side dependency to confirm first.)

---

## [1.4.3]

### Added

- **Backend refactor — Phase 1 options validation (X3).** Startup validators (`IValidateOptions` + `ValidateOnStart`) for `OperatorAiOptions`, `AiServiceOptions`, and `VideoLoungeOptions`: a misconfigured bounded value — non-positive parallelism / top-K / token cap / timeout, a routing score outside `[0,1]`, a map temperature outside `[0,2]`, or a sub-32-byte HMAC secret under real (non-stub) video-lounge signing — now **fails fast at boot** instead of being silently clamped at ~30 call sites. The shipped defaults are validated in range; `VideoLoungeOptions` moved from `Configure<>` to `AddOptions().BindConfiguration().ValidateOnStart()`. With unit tests + a boot smoke.

---

## [1.4.2]

### Fixed

- **Backend refactor — Phase 1 security hardening (each with a regression test).** (1) `OutboundUrlAllowlist` SSRF guard now also blocks IPv4-mapped IPv6 (`::ffff:10.0.0.1`, normalized to IPv4 before the private-range check), IPv6 ULA (`fc00::/7`), the IPv6 unspecified address (`::`), `0.0.0.0/8`, and CGNAT (`100.64/10`); genuine public IPv4/IPv6 hosts stay allowed (literal-IP only — DNS rebinding remains the fetching worker's responsibility, noted in code). (2) `ECDSAKeyService` now **fails fast** when `Jwt:SigningPemPath` is configured but the file is missing in any non-Development environment, instead of silently falling back to an ephemeral key (which breaks JWKS stability and token persistence across restarts); Development keeps the ephemeral fallback.

---

## [1.4.1]

### Added

- **Backend refactor — Phase 0a (test safety nets, no behaviour change).** Following the whole-backend analysis (`docs/prompts/backend-code-analysis-and-refactor-v1-agent-prompt.md`), added **39 characterization edge-case tests** for previously-untested security-critical units: `FaceScopeContext.ResolveDataFaceId` (cross-tenant anti-spoof), `VideoLoungeTokenService` (stub-vs-signed, TTL clamp, join-mode→publish-grant — the Listener-can-publish-video case is documented + flagged `// REVIEW` for the Phase 1 fix), `SearchHitAclFilter` (tenant-isolation: soft-removed/inactive/banned/nonexistent rows hidden), `ContentRetentionCleanupService` (dry-run counts == execute, persists nothing on dry-run, clears AI-review fields on execute), the operator mail/push secret protectors (round-trip + cross-purpose isolation), and `FirebaseServiceAccountValidator` (untrusted-JSON validation, every rejection branch).
- Test-time infrastructure under `BeDemo.Api.Tests/TestDoubles/`: a deterministic `FakeClock` (`IClock` seam) and an `InMemoryDb` helper; a `coverage.runsettings` (Cobertura, generated/migration code excluded) so coverage becomes measurable.
- Architecture Decision Records `docs/adr/0001-backend-refactor-program.md` (phased, discipline-gated program) and `docs/adr/0002-test-safety-nets-and-determinism.md`.

### Changed

### Fixed

- **Backend refactor — Phase 1 bug fixes (each with a regression test).** (1) `VideoLoungeTokenService`: a Listener join now publishes audio only — the LiveKit grant carries `canPublishSources` (`microphone` for Listener, `camera`+`microphone` for Full) so a Listener can no longer publish video (the `canPublishVideo` distinction was previously computed and dropped). (2) `OperatorAiLiveStatsPrefetcher`: the per-bundle results map is now a `ConcurrentDictionary` — it is written from parallel `Task.WhenAll` tasks and a plain `Dictionary` is not thread-safe even for distinct keys. (3) `Routing.IsExemptFromFaceScope`: the `/api/*` face-scope exemptions are now segment-aware, so a path like `/api/profile-evil` or `/api/profiles` no longer inherits the `/api/profile` exemption and bypasses tenant enforcement (`/swagger`/`/openapi`/`/favicon` keep a plain prefix match). (4) `OperatorAiResponseGuard`: dropped the bare `"grpc"` infrastructure marker that false-positived any legitimate answer mentioning gRPC; real transport failures still surface via the `Error:` prefix / `ai service unavailable` markers.

---

## [1.4.0]

### Added

- **Operator AI 7B performance optimizations.** Fewer generations, faster generations, and token streaming for the local-7B operator chat. A **deterministic count fast-path** (`OperatorAiStatsIntent.IsSimpleCountQuestion` + `OperatorAiCountFastPath`) answers a simple single-metric count over one bundle with **0** generations; a **single-bundle fast-path** answers a one-bundle question with **1** generation and no synthesis (both via the orchestrator's new `PrepareSelectedAsync` terminal-plan seam, which also carries a per-turn stage trace — fast-path, generation count, load/map latencies).
- **End-to-end token streaming** (`IAiGrpcService.GenerateStreamAsync` over the worker `GenerateStream` RPC; `IOperatorAiStreamingSkill` + `StatsSkill.RunStreamingAsync`; `ChatHub` emits `OperatorAiMessageDelta` SignalR events and persists the full message once on completion). Gated by `OperatorAi:StreamingEnabled` (default on) with a clean non-streaming fallback.
- **CPU-resident helper model (O19)** via `AiServiceOptions.HelperModel`: an `IOperatorAiDecisionHelper` makes the small routing/gating decisions (simple-count gate, report-type) with the deterministic heuristic as the fallback (advisory only — never alters numbers); plus an experimental heterogeneous parallel map behind `OperatorAi:HelperParallelMapEnabled` (default off). Per-call `temperature` / `stop` / `model` overrides added to `GenerateAsync` and the proto `GenerateRequest`.
- **Safety + observability:** a single-active-generation guard (`IOperatorAiActiveGenerationGuard`, `OperatorAi:SingleActiveGenerationGuardEnabled`) rejecting overlapping turns per conversation; an optional exact-repeat answer cache (`IOperatorAiAnswerCache`, `OperatorAi:AnswerCacheEnabled`, default off); a startup warm service (`OperatorAiStartupWarmService`) that warms the 4 skill vectors and issues one throwaway Generate to preload the model; and embed-once per turn (the router seeds the retriever's query-embedding cache).
- New `OperatorAi:*` knobs (`MapTemperature`, `MapStopSequences`, `StreamingEnabled`, `SingleActiveGenerationGuardEnabled`, `AnswerCache*`, `HelperForDecisions`, `HelperParallelMapEnabled`) and `AiService:*` knobs (`HelperModel`, `HelperTimeoutMs`, `WarmUpGenerationOnStartup`, `WarmUpStartupTimeoutSeconds`).
- Extended edge-case tests (PF-1…PF-20): strict count-intent detection, count + single-bundle fast-paths (0/1 generations), map token-cap/temperature/stop, the count formatter, the single-flight guard, the answer cache (off/on/clear), the decision-helper fallback, the streaming skill (delta forwarding + worker-error fallback + zero-hit), and router embed-once. Full suite **1761 passing**.

### Changed

- Per-bundle map generations now use a **96-token cap** (`LiveBundleMaxNewTokens`), **low temperature 0.2** + stop sequences, and a trimmed prompt; the default map parallelism is **1** (`MaxParallelBundleAiCalls`) since a single Ollama serialises map calls anyway. Timeout budgets retuned to the measured ~90–120s baseline (per-bundle 30s, overall turn 180s). Public-stats snapshot parallelism is decoupled from the operator-chat default.

### Fixed

---

## [1.3.0]

### Added

- **Operator AI skills.** The operator chat front door now routes each request to one **skill** and runs it. New `IOperatorAiSkill` framework (`OperatorAiSkillRequest`/`Result`/`Trace`/`Trust`), `IOperatorAiSkillRegistry`, and an `IOperatorAiSkillRouter` that routes by **in-memory cosine** over the 4 skill descriptor vectors (cached in a singleton `IOperatorAiSkillVectorCache`, re-warmed on embed-model change) — below `OperatorAi:SkillRoutingMinScore` (default 0.35) or when embeddings are unavailable it falls back to general-assistant (never a hard refusal). No per-skill ACL (single SUPER_ADMIN gate); skills live in the backend (worker stays thin).
- Four skills: **stats** (a thin wrapper over the shipped RAG v1 retrieve→map→stitch — behaviour unchanged), **reports** (detect type → assemble aggregates → worker `GenerateReport`, 3 deterministic types), **moderation** (aggregate moderation metrics Q&A — trusted, no raw content), and **general-assistant** (context-free fallback reply).
- `IAiGrpcService.GenerateReportAsync` wraps the worker `GenerateReport` RPC (AI-UP11). `OperatorAi:SkillRoutingMinScore` config.
- Extended edge-case tests: router (routing/threshold-boundary/threshold-above-max/embed-unavailable, cosine + dimension-mismatch), registry (case-insensitive resolution, fallback guarantee), vector-cache (warm-once + model-change re-warm + all-fail→null); stats (zero-hit/happy/fallback trace); reports (type detection across cases, moderation-backlog/face_health/grid_completeness assembly via EF, ambiguous + worker-error); moderation (aggregate-only, empty→deterministic fallback, no-raw-content guarantee); general-assistant (no-fabrication prompt + token cap); and per-skill trust declarations. Full suite **1736 passing**.

### Changed

- `ChatHub` operator branch is now `route → skill.RunAsync → persist`; the direct RAG selection + map/stitch moved into `StatsSkill`. The operator-visible contract (one English message) is unchanged.

---

## [1.2.0]

### Added

- **Operator AI RAG retrieval.** Embedding retrieval (Elasticsearch kNN + BM25, RRF) replaces the LLM planner for selecting which stat bundles answer an operator question; the per-bundle map + stitch is retained. New `ISearchWorkerKnowledgeClient` (4 `SearchService` RPCs: `IndexKnowledge`/`DeleteKnowledge`/`SemanticSearch`/`KnowledgeIndexStatus`), `IOperatorAiKnowledgeIndexer` (+ startup hosted service, single-flight, content-hash idempotency), `IOperatorAiRetriever` (query-embedding cache, readiness gate → planner fallback, zero-hit escalation), and `IOperatorAiPlannerFallbackSelector` (the legacy planner, now the degraded fallback). `EmbedText` (AI-UP15) is now consumed.
- `AiService:EmbeddingModel` + `EmbeddingDim` single source of truth with a startup probe assertion; new `OperatorAi:*` knobs (`MinRetrievalScore`, `ZeroHitRetryAttempts`, `QueryEmbeddingCacheTtlSeconds`, `Embed/RetrievalTimeoutMs`, `OverallTurnBudgetMs`, `RetrievalTraceEnabled`, `LiveStatsDebugJson`).
- Admin endpoints `POST /api/operator-ai/knowledge/reindex` and `GET /api/operator-ai/knowledge/status` (`CanManageAllFaces`); admin i18n labels for the knowledge panel (en/sk/cs/de/fr/it).
- Per-bundle authored RAG descriptors (synonyms + sample questions) for all 61 stat bundles, with a coverage lint test.
- Extended edge-case tests: retriever ordering/fallback/zero-hit escalation + query-embedding cache; status-cache readiness drift + caching; indexer single-flight coalesce, content-hash idempotency, embed/worker-unavailable and partial-failure paths; reindex/status controller (Forbid/409/503/Ok); orchestrator per-bundle timeout partial-stitch + coverage note. Full suite **1697 passing**.

### Changed

- Operator AI chat is now **always data-grounded**: the unified live orchestrator runs `select → fresh-load K → map → stitch`, selecting via RAG (planner only as the degraded fallback). Only the selected bundles are loaded (the always-all-61 prefetch is dropped).

### Removed

- Operator chat `off` and `inline` stats modes and the `responseLocale` argument (`SendToAiWithOperatorStats` is now `(conversationId, message, maxParallelBundleAiCalls?)`); the AI answers in English. The infra-failure message no longer references the removed `inline` mode.

### Fixed

---

## [1.1.0]

### Added

- **Runtime performance v1 (BE-RP1…BE-RP35):** JWT `AccessTokenVersion` cache, faces config
  cache with `AsSplitQuery`, capabilities cache, messenger SQL pagination envelope,
  platform stats cache decorator, search outbox parallel/bulk gRPC, admin autocomplete
  cache, async face routing load, grid snapshot BFF (`/api/faces/{id}/grid-snapshot`) with
  shared grid list services, upload serve `Cache-Control`/`ETag`, hub user display cache,
  `Performance` appsettings section, baseline script (`scripts/backend-perf-baseline.mjs`),
  k6 load stub (`scripts/backend-load-test.k6.js`), docs
  (`docs/runtime-performance-v1.md`, guides, SVG flow diagram).
- Edge-case tests: BE-RP1/3/4/22/26/28/34 performance and grid contract suites.

### Changed

- `GET /api/messages/conversations` returns paginated envelope `{ items, page, pageSize,
totalCount, totalPages }` (BE-RP3).
- `FacesController.GetFacesConfig` delegates to `FacesConfigService` with generation-based
  cache invalidation.

---

## [1.0.2]

### Added

- Add README shield badges (version, CI, stack tech) via sync-readme-badges.py.

### Added

- Add README shield badges (version, CI, stack tech) via sync-readme-badges.py.

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

[Unreleased]: https://github.com/01laky/many_faces_backend/compare/v1.4.4...HEAD
[1.0.2]: https://github.com/01laky/many_faces_backend/compare/v1.0.1...v1.0.2
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
[1.2.0]: https://github.com/01laky/many_faces_backend/compare/v1.1.0...v1.2.0
[1.4.12]: https://github.com/01laky/many_faces_backend/compare/v1.4.11...v1.4.12
[1.4.11]: https://github.com/01laky/many_faces_backend/compare/v1.4.10...v1.4.11
[1.4.10]: https://github.com/01laky/many_faces_backend/compare/v1.4.9...v1.4.10
[1.4.9]: https://github.com/01laky/many_faces_backend/compare/v1.4.8...v1.4.9
[1.4.8]: https://github.com/01laky/many_faces_backend/compare/v1.4.7...v1.4.8
[1.4.7]: https://github.com/01laky/many_faces_backend/compare/v1.4.6...v1.4.7
[1.4.6]: https://github.com/01laky/many_faces_backend/compare/v1.4.5...v1.4.6
[1.4.5]: https://github.com/01laky/many_faces_backend/compare/v1.4.4...v1.4.5
[1.4.4]: https://github.com/01laky/many_faces_backend/compare/v1.4.3...v1.4.4
[1.4.3]: https://github.com/01laky/many_faces_backend/compare/v1.4.2...v1.4.3
[1.4.2]: https://github.com/01laky/many_faces_backend/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/01laky/many_faces_backend/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/01laky/many_faces_backend/compare/v1.3.0...v1.4.0
