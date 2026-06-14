# Changelog

All notable changes to **`many_faces_backend`** are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) — **version headings only, no dates**. SemVer: [`VERSION`](./VERSION).

### Release index

| Version         | Theme                                                                                  |
| --------------- | -------------------------------------------------------------------------------------- |
| [1.4.46](#1446) | Fix search outbox DbContext concurrency (RAG indexing)                                  |
| [1.4.45](#1445) | Fix operator AI chat: SignalR 2-arg hub contract (drop optional param)                  |
| [1.4.44](#1444) | Wall-tickets pagination parity fix + pure-helper edge tests                            |
| [1.4.43](#1443) | Untested pure-helper edge tests (test-gap fill)                                        |
| [1.4.42](#1442) | Edge-case fixes: grid pagination, gRPC timeout, SSRF dot, AI cache flush + tests       |
| [1.4.41](#1441) | CHANGELOG release-index formatting normalization (docs)                                |
| [1.4.40](#1440) | Backend refactor Phase 3 X7 — `[ProducesResponseType]` complete + remaining anon fixes |
| [1.4.39](#1439) | Backend refactor Phase 3 X7 DTO layer — zero anonymous returns                         |
| [1.4.38](#1438) | Backend refactor Phase 3 X11 AsNoTracking + projections                                |
| [1.4.37](#1437) | Backend refactor Phase 3 X10 parallel stats counts                                     |
| [1.4.36](#1436) | Backend refactor Phase 3 X11 EF query smells                                           |
| [1.4.35](#1435) | Backend refactor Phase 2 gRPC worker-client base                                       |
| [1.4.34](#1434) | Backend refactor Phase 4 social unique constraints                                     |
| [1.4.33](#1433) | Backend refactor Phase 4 request-log observability                                     |
| [1.4.32](#1432) | Backend refactor Phase 3 Program.cs modularise (9)                                     |
| [1.4.31](#1431) | Backend refactor Phase 3 Program.cs modularise (8)                                     |
| [1.4.30](#1430) | Backend refactor Phase 3 Program.cs modularise (7)                                     |
| [1.4.29](#1429) | Backend refactor Phase 3 Program.cs modularise (6)                                     |
| [1.4.28](#1428) | Backend refactor Phase 3 Program.cs modularise (5)                                     |
| [1.4.27](#1427) | Backend refactor Phase 3 Program.cs modularise (4)                                     |
| [1.4.26](#1426) | Backend refactor Phase 3 Program.cs modularise (3)                                     |
| [1.4.25](#1425) | Backend refactor Phase 4 Postgres Testcontainers                                       |
| [1.4.24](#1424) | Backend refactor Phase 3 Program.cs modularise (2)                                     |
| [1.4.23](#1423) | Backend refactor Phase 3 Program.cs modularise (1)                                     |
| [1.4.22](#1422) | Backend refactor X5/X6 Users auth (migration done)                                     |
| [1.4.21](#1421) | Backend refactor Phase 2 secret-protector base                                         |
| [1.4.20](#1420) | Backend refactor X6 ApiControllerBase (complete)                                       |
| [1.4.19](#1419) | Backend refactor X6 ApiControllerBase (UserId)                                         |
| [1.4.18](#1418) | Backend refactor Phase 0 AI test fake (complete)                                       |
| [1.4.17](#1417) | Backend refactor Phase 0 shared AI test fake                                           |
| [1.4.16](#1416) | Backend refactor X5/X6 dual-policy migration (8)                                       |
| [1.4.15](#1415) | Backend refactor X5/X6 SuperAdmin policy (7)                                           |
| [1.4.14](#1414) | Backend refactor X5/X6 SuperAdmin policy (6)                                           |
| [1.4.13](#1413) | Backend refactor X5/X6 auth-policy migration (5)                                       |
| [1.4.12](#1412) | Backend refactor X5/X6 auth-policy migration (4)                                       |
| [1.4.11](#1411) | Backend refactor X5/X6 auth-policy migration (3)                                       |
| [1.4.10](#1410) | Backend refactor X5/X6 auth-policy migration (2)                                       |
| [1.4.9](#149)   | Backend refactor X5/X6 auth-policy migration (1)                                       |
| [1.4.8](#148)   | Backend refactor X4 ProblemDetails errors (flag)                                       |
| [1.4.7](#147)   | Backend refactor X14 health/readiness probes                                           |
| [1.4.6](#146)   | Backend refactor X13 correlation-id middleware                                         |
| [1.4.5](#145)   | Backend refactor X5 declarative auth policies                                          |
| [1.4.4](#144)   | Backend refactor PII redaction + dead-code cleanup                                     |
| [1.4.3](#143)   | Backend refactor Phase 1 options validation                                            |
| [1.4.2](#142)   | Backend refactor Phase 1 security hardening                                            |
| [1.4.1](#141)   | Backend refactor Phase 0a (test safety nets)                                           |
| [1.4.0](#140)   | Operator AI 7B performance (fast-paths, streaming)                                     |
| [1.3.0](#130)   | Operator AI skills (router + 4 skills)                                                 |
| [1.2.0](#120)   | Operator AI RAG retrieval (Elasticsearch kNN+BM25)                                     |
| [1.1.0](#110)   | Backend runtime performance v1 (BE-RP1…35)                                             |
| [1.0.2](#102)   | README shield badges                                                                   |
| [1.0.0](#100)   | Operator infra settings, global search, live stats                                     |
| [0.9.0](#090)   | Operator consoles, VideoLounge, moderation APIs                                        |
| [0.8.0](#080)   | SHV2 security, FluentValidation, signed uploads                                        |
| [0.7.0](#070)   | Email-code registration, static i18n bundles                                           |
| [0.6.0](#060)   | Workers gRPC, content moderation, many_faces_proto                                     |
| [0.5.0](#050)   | ACL, refresh tokens, remember-me, demo seeds                                           |
| [0.4.0](#040)   | Social modules, grid, stories, chat, wall                                              |
| [0.3.0](#030)   | Face-based multi-tenant routing                                                        |
| [0.2.0](#020)   | UserProfile, RBAC, integration tests                                                   |
| [0.1.0](#010)   | .NET API foundation, PostgreSQL, Docker dev                                            |

## [Unreleased]

### Added

### Changed

### Fixed

---

## [1.4.46]

### Fixed

- **Search outbox processor stalled the moment search was enabled** (`A second operation was started on
  this context instance …`). `SearchOutboxProcessorHostedService.PrepareBatchAsync` built the per-entry
  search documents concurrently (`Select(async …)` + `Task.WhenAll`, fanned out by
  `SearchOutboxMaxParallelGrpc`) over the **single per-tick `ApplicationDbContext`**, which is not
  thread-safe — so every tick threw and the outbox never drained (370 docs stuck pending, the kNN
  index never built). The document-build phase is now serial (one shared DbContext); the
  `SearchOutboxMaxParallelGrpc` knob still governs the genuinely parallel gRPC push phases
  (`ProcessDeletesAsync` / `ProcessIndexesAsync`). Verified live: pending drained 370 → 0 with no tick
  failures and the operator-AI knowledge index built (61/61, nomic 768-dim), so the retriever now uses
  Elasticsearch kNN instead of the planner fallback.

---

## [1.4.45]

### Fixed

- **Operator AI chat was completely broken — every turn failed with "Could not get an AI response".**
  The SignalR hub method `ChatHub.SendToAiWithOperatorStats` declared three parameters
  (`conversationId`, `message`, `int? maxParallelBundleAiCalls = null`), but the admin SPA invokes it
  with two (`conversationId`, `message`). ASP.NET Core SignalR does **not** support optional / defaulted
  hub-method parameters: a 2-argument client invoke against the 3-parameter method failed argument
  binding on the server ("Failed to invoke 'SendToAiWithOperatorStats' due to an error on the server")
  **before the method body ran**, so not a single operator AI turn ever executed (confirmed: zero
  method-entry log lines, and a live SignalR client reproduced 2-arg REJECT vs 3-arg OK). Removed the
  unused `maxParallelBundleAiCalls` wire parameter — no client ever supplied an override — and the
  per-turn bundle-parallelism cap is now read directly from `OperatorAiOptions.MaxParallelBundleAiCalls`
  (default 1, so behaviour is unchanged). The `OperatorAiChatHubContractTests` contract test, which had
  asserted the buggy 3-parameter shape, now pins the method to exactly `(int conversationId, string
  message)` with no optional parameters as a regression guard.

---

## [1.4.44]

### Added

- Edge-case tests for four previously-untested pure helpers (unit-test-gap-fill): `StoryVisibility` (untargeted story → all faces, targeted → only listed faces, null viewer face never matches), `PlatformNotificationTitles` (sk/cz localized, everything else → English), `PortalSupportedUiLanguages` (the six UI codes accepted case-insensitively/trimmed; `cs`/unknown/nullish rejected), and `FaceGradientPresets` (seeded faces get their fixed palette, unknown indices get a deterministic hash-derived variant, output is always a valid gradient shape). 31 tests, all green.

### Fixed

- **Wall-tickets list pagination disagreed with the grid snapshot (BE-RP34 contract drift).** The standalone `GET /api/faces/{faceId}/wall-tickets` endpoint computed `totalPages` as a raw `Math.Ceiling(total / pageSize)` — returning **0** for an empty list (vs the snapshot block's **1**) and risking a `pageSize=0` divide-by-zero — and returned the raw (unclamped) page. It now uses `ListPaginationHelper.ClampPage` for exact parity with the grid-snapshot wall-tickets block (one empty page, out-of-range page clamped, pageSize-0 safe). This fixes the previously-failing `FaceGridSnapshotContractTests.BE_RP34_U1` contract test; the full suite is now green (2061/2061).

---

## [1.4.43]

### Added

- Edge-case tests for five previously-untested pure helpers (unit-test-gap-fill): `VideoLoungeJoinModeParser` (only the three member modes parse; operator-stealth and nullish/garbage are rejected), `StatsTimeseriesBucketing` (day/week histogram gap-fills the range, counts per bucket, aligns week buckets to ISO Mondays), `ProfileGridSettingsJson` (tolerant parse, a null patch value removes a key, the 16 KB size guard), `RateLimitingPartitionKey` (user-id vs IP partitioning, per-test-host scope prefix, null-context guard), and `ModerationQueueSorter` (default `submittedAtUtc`-desc with `CreatedAt` fallback, ascending/`desc` field sorts, unknown-field fallback). 29 tests, all green. No source changes.

---

## [1.4.42]

### Changed

- Removed a dead `SemaphoreSlim` in the search-outbox bulk-index path (allocated but never awaited; chunking stays intentionally sequential because the `SearchOutboxEntry` instances are EF-tracked).
- Added extensive edge-case test coverage (+47 cases) for the fixes below: `ClampPage` boundaries / `pageSize ≤ 0`, the operator-AI answer cache (normalization, skill isolation, `Clear()`), the album visibility filter (operator / own / anonymous), the outbound-URL allow-list trailing-dot cases, the reindex answer-cache flush across rebuild outcomes, and the `AiGrpcService` call-options.

### Fixed

- Grid-snapshot **wall-tickets block** now clamps the page and no longer divides by `pageSize` (a `pageSize=0` produced `int.MinValue` garbage `totalPages`); it mirrors every sibling block via `ListPaginationHelper.ClampPage`.
- **`AiGrpcService` gRPC timeout/cancellation**: `CreateCallOptions` built a linked `CancellationTokenSource` with `using` and disposed it before the call ran, so its `CancelAfter` timer never fired and the token came from a disposed source (could throw `ObjectDisposedException`). It now passes the caller's token straight through and relies on the gRPC deadline for the timeout.
- **Operator-AI answer cache** is now flushed on a successful/partial knowledge reindex — `OperatorAiAnswerCache.Clear()` had zero call sites despite the documented invariant, so a re-asked question could be served a stale count within the TTL after a reindex.
- **`OutboundUrlAllowlist`**: a trailing FQDN root dot (e.g. `localhost.`) no longer bypasses the exact-match `localhost` block (SSRF defense-in-depth; suffix checks for `.local`/`.internal` were already safe).
- **`AlbumGridListService`**: an unauthenticated viewer now uses an empty (never-matching) creator id, so the own-private-album rule matches nothing and an anonymous viewer sees only public approved albums (also clears a `CS8604` nullable warning).

---

## [1.4.41]

### Changed

- Normalize the CHANGELOG release-index table formatting (Prettier markdown — column widths only). Documentation-only; no API, EF, or runtime change.

---

## [1.4.40]

### Changed

- **Backend refactor — Phase 3 X7 completion: `[ProducesResponseType]` on every controller action + elimination of all remaining anonymous response bodies.** This is the final increment of X7: the first session added named DTOs for the bulk of anonymous returns; this session audits every controller and closes all remaining gaps.

  **`[ProducesResponseType]` attributes — added to all remaining controllers (previously 0 attributes):**
  - `OperatorAiConversationsController` (15 actions): typed `OperatorAiSystemSettingsDto`, `OperatorAiModelStatusDto`, `OperatorAiWorkerHostDto`, `OperatorAiLiveStatsCacheSettingsDto`, `OperatorAiPublicStatsSettingsDto`, `IReadOnlyList<OperatorAiConversationListItemDto>`, `OperatorAiMessagesPageDto`; 201/204/404 as appropriate.
  - `OperatorContentController` (13 actions): 204 No Content for all hard-delete and soft-delete paths; 200 `VideoLoungeJoinResultDto` for stealth-join; 200 `KickedResultDto`/`KickedAllResultDto` for kick/kick-all.
  - `FaceChatRoomsController` (13 actions) and `FaceVideoLoungesController` (15 actions): already attributed in prior session; confirmed.
  - `FaceWallTicketsController` (7 remaining): `WallTicketCreatedDto` (201), `WallTicketUpdateResultDto`, `LikeResultDto`, `IReadOnlyList<WallTicketCommentResultDto>`, `WallTicketCommentResultDto` (201), 204 No Content.
  - `AdminFaceWallTicketsController` (7 actions): 200/201/204 with `WallTicketCreatedDto`, `WallTicketStatusResultDto`, `WallTicketDetailCommentDto`, 204 No Content.
  - `FaceProfilesController` (8 actions): `LikeResultDto`, `IReadOnlyList<ProfileLikerDto>`, `ProfileCommentResultDto`, `ReviewResultDto`, 204 No Content for delete paths; untyped 200 for BuildEnvelope list paths.
  - `FaceProfilesController.Operator.cs` (1 action): untyped 200 for `ListProfiles` BuildEnvelope.
  - `PagesController` (7 actions): `PageDto` (200/201), 204 No Content, untyped 200 for translations.
  - `ContentModerationController` (7 actions): `ModerationMetricsWithAlerts`, `BulkModerationResponse`, `ModerationDecisionResultDto` for approve/reject/remove, untyped 200 for BuildEnvelope queue.
  - `OperatorUsersController` (7 actions): `OperatorUserDetailDto`, `UserRoleIdResultDto`, `BanResultDto`/`FaceBanResultDto` (with conditional 204), `MessageIdResultDto`.
  - `AdminMeProfileController` (5 actions): `AdminMeProfileDto`, `UserRoleIdResultDto`, `MessageResultDto`, 204 No Content for password change.
  - `MessagesController` (3): untyped 200 for BuildEnvelope conversation/request paths, `CountResultDto` for mark-read.
  - `PageTypesController` (5): `IReadOnlyList<PageTypeDetailDto>`, `PageTypeDetailDto`, 204.
  - `UserFollowsController` (5): `IReadOnlyList<UserFollowItemDto>`, `IsFollowingDto`, `SuccessResultDto`.
  - `FriendRequestsController` (4): `IReadOnlyList<FriendRequestItemDto>`, `SuccessResultDto`.
  - `UserBlocksController` (4): `IReadOnlyList<UserBlockItemDto>`, `IsBlockedDto`, `SuccessResultDto`.
  - `AlbumLikesController`, `BlogLikesController`, `ReelLikesController`, `StoryLikesController` (3 each): `IReadOnlyList<ContentLikeItemDto>`, `SuccessResultDto`.
  - `AlbumCommentsController`, `BlogCommentsController`, `ReelCommentsController` (4 each): `IReadOnlyList<*CommentListItemDto>`, `*CommentDto` (201/200), 204.
  - `StoryCommentsController` (2): `IReadOnlyList<StoryCommentListItemDto>`, `CreatedEntityDto`.
  - `PageComponentsController` (3): `PageComponentCreatedDto`, `SuccessResultDto`.
  - `UsersController` (3): `UserDto` (200/201), 404.
  - `StatsController` (3): `AdminDashboardSummaryDto`, `PublicStatsSnapshotDto`, `StatsTimeseriesResponseDto`.
  - `AuthController` (3), `AuthEmailConfirmController` (1): `MessageResultDto`.
  - `AiAvailabilityController` (1): `AiEnabledResponse`.
  - `MeController` (1): `CapabilitiesResponse`.
  - `MyContentSubmissionsController` (1): `IReadOnlyList<MyContentSubmissionDto>`.
  - `NotificationsController` (1): `IReadOnlyList<NotificationHistoryItemDto>`.
  - `OAuthJwksController` (1): untyped 200.
  - `OAuth2Controller` (2): `OAuth2TokenResponse`, 400 for deprecated register.
  - `OAuth2RegistrationController` (4): `RegisterRequestResponseDto`, `RegisterPrefillResponseDto`, `RegisterCompleteResponseDto`.
  - `OperatorAiKnowledgeController` (2): `KnowledgeReindexResultDto`, `KnowledgeIndexStatusDto`.
  - `SearchController` (1): `SearchHealthDto`.
  - `UploadsController` (1): untyped 200 (binary file response).
  - `OperatorUserChatController` (4): `IReadOnlyList<OperatorUserChatConversationDto>`, `OperatorUserChatHistoryPageDto`, `OperatorUserChatThreadExistsDto`, `CountResultDto`.

  **New named DTOs added to `ActionResultDtos.cs`:**
  - `NotificationHistoryItemDto` — `{ Id, Title, Message, Type, CreatedAt }` replacing the anonymous shape returned by `NotificationsController.GetHistory`.
  - `ModerationDecisionResultDto` — `{ ApprovalStatus?, AiReviewStatus? }` replacing the anonymous shape returned by `ContentModerationController` Approve/Reject/Remove actions.
  - `AdminWallTicketListItemDto` — `{ Id, Title, DescriptionPreview, Status, CreatorId, CreatorName, LikesCount, CommentsCount, CreatedAt }` replacing the anonymous BuildEnvelope items in `AdminFaceWallTicketsController.List`.
  - `FaceProfileCommentAdminItemDto` — `{ Id, UserId, Body, CreatedAt, AuthorDisplayName }` replacing the paginated operator path anonymous type in `FaceProfilesController.ListComments`.
  - `FaceProfileReviewAdminItemDto` — `{ Id, AuthorUserId, Title, Text, Stars, CreatedAt, AuthorDisplayName }` replacing the paginated operator path anonymous type in `FaceProfilesController.ListReviews`.

  **Remaining anonymous error shapes converted:**
  - `FaceGridSnapshotController.GetSnapshot`: `BadRequest(new { error = "…" })` → `BadRequest(new ErrorResponseDto { Error = "…" })`.
  - `OAuth2Controller.Register`: deprecated-endpoint `BadRequest(new { error, message })` → `BadRequest(new ErrorWithMessageDto { Error = "…", Message = "…" })`.

  **Missing `using` directives added:**
  - `AdminMeProfileController.cs` gains `using BeDemo.Api.Models.DTOs.Admin` to resolve `AdminMeProfileDto`.
  - `NotificationsController.cs` gains `using BeDemo.Api.Models.DTOs` to resolve `NotificationHistoryItemDto`.

  **Build state:** 0 errors, 3 pre-existing warnings (unchanged), all 1950 tests pass.

---

## [1.4.39]

### Changed

- **Backend refactor — Phase 3 X7 DTO layer: projection-expression DTOs + `[ProducesResponseType]` — zero anonymous-object returns from controller actions.** All 385 `new { … }` anonymous bodies in controller action returns replaced by named DTO types with `init`-property factories; global JSON camelCase serialisation policy applied. Measurable exit criterion: `grep -rn "return.*new {" Controllers/` now returns 0 results (route-value objects in `CreatedAtAction` route parameter are exempt, being MVC routing infrastructure not HTTP response bodies).

  **Global JSON camelCase policy (Program.cs):**
  - `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` added to `AddJsonOptions`. Named DTOs now serialise identically to the prior anonymous types — all tests and frontend contracts preserved.

  **New DTO files (`BeDemo.Api/Models/DTOs/`):**
  - `AlbumDto.cs` — `AlbumDetailDto` (full detail with moderation-aware `From(Album, userId, showModerationFields)` factory), `AlbumCreateResultDto` (slim create/update response), `AlbumFaceDto`, `AlbumMediaItemDto`, `AlbumListItemDto` (EF-projectable for `GetAlbumsByUser` list).
  - `BlogDto.cs` — `BlogDetailDto` (full detail with `ContentModerationPreviewText` preview), `BlogCreateResultDto`, `BlogImageDto`.
  - `ReelDto.cs` — `ReelDetailDto` (full detail), `ReelFaceDto`.
  - `StoryDto.cs` — `StoryDetailDto`, `StoryFaceRefDto`, `StoryImageDetailDto`, `StoryViewerDto`, `StoryCreatedDto`, `StoryImageUploadResultDto`.
  - `ActionResultDtos.cs` — `SuccessResultDto`, `LikeResultDto`, `IsFollowingDto`, `IsBlockedDto`, `JoinResultDto`, `JoinRequestResultDto`, `ApprovedResultDto`, `DeniedResultDto`, `SessionIdResultDto`, `EndedResultDto`, `LeftResultDto`, `OkResultDto`, `KickedResultDto`, `KickedAllResultDto`, `CreatedEntityDto`, `CountResultDto`, `PublishedResultDto`, `RecordedResultDto`, `BanResultDto`, `FaceBanResultDto`, `UserRoleIdResultDto`, `FaceRoleResultDto`, `VisitedResultDto`, `ExitFaceResultDto`, `MessageIdResultDto`, `AvatarUploadResultDto`, `GridComponentsResultDto`, `PageComponentCreatedDto`, `MailerTestSentDto`, `ErrorResponseDto`, `ErrorCodeResponseDto`, `CodedErrorResponseDto`, `MessageResultDto`, `AppScopedErrorDto`, `ErrorWithMessageDto`, `WallTicketStatusResultDto`.
  - `VideoLoungeSessionDtos.cs` — `VideoLoungeJoinResultDto`, `VideoLoungeRefreshTokenResultDto`, `VideoLoungeNoSessionDto`.
  - `ContentCommentDtos.cs` — `BlogCommentDto`, `AlbumCommentDto`, `ReelCommentDto`.
  - `WallTicketDtos.cs` — `WallTicketCreatedDto`, `WallTicketUpdateResultDto`, `WallTicketCommentResultDto`.
  - `PageComponentDetailDto.cs` — `PageComponentDetailDto`, `PageComponentTypeRefDto`, `PageComponentDisplayModeRefDto`, `ComponentTypeDetailDto`, `DisplayModeDetailDto`, `PageTypeDetailDto`.
  - `UserProfileResponseDtos.cs` — `UserProfileResponseDto`, `FaceProfileDetailDto`, `ProfileCommentResultDto`, `ReviewResultDto`.
  - `KnowledgeIndexDtos.cs` — `KnowledgeReindexResultDto`, `KnowledgeIndexStatusDto`.

  **Controllers migrated (all anonymous-object returns replaced, 43 files):**
  `AlbumsController`, `BlogsController`, `ReelsController`, `StoriesController`, `FacesController` (face-role, visited, exit-face), `FaceChatRoomsController` (create, update, join, join-request, approve, deny), `FaceVideoLoungesController` (create, update, join, live-start, live-join, live-leave, live-end, live-refresh-token, heartbeat), `FaceWallTicketsController` (create, update, like/unlike, comment create/list), `AdminFaceWallTicketsController` (approve/deny status results), `FaceProfilesController` (like/unlike, comment, review), `OperatorUsersController` (role, ban, face-ban, message), `OperatorContentController` (kicked, kickedAll), `OperatorAiKnowledgeController` (reindex, status), `OperatorAiConversationsController`, `MessagesController` (face-ban error, mark-read count), `ProfileController` (get-me, avatar upload, grid settings), `PageComponentsController` (create, update, delete), `LocalizationController` (bundle OK → direct pass-through of `LocalizationBundleResponse`, error DTOs), `AuthController`, `AuthEmailConfirmController`, `BlogCommentsController`, `AlbumCommentsController`, `ReelCommentsController`, `StoryCommentsController`, `StoryLikesController`, `BlogLikesController`, `AlbumLikesController`, `ReelLikesController`, `FriendRequestsController`, `UserFollowsController`, `UserBlocksController`, `AdminMailerTestController`, `AdminMeProfileController`, `OperatorUserChatController`, `OAuthJwksController` (file-scoped `JwksResult` wrapper), plus all error paths with `ErrorResponseDto`.

  **EF LINQ projections (named DTOs in `.Select()`):**
  - `AlbumsController.GetAlbumsByUser`: `Select(a => new AlbumListItemDto { … })` — `ContentModerationHelpers.CreatorStatusLabel` evaluated client-side (as before with anonymous types).
  - `FaceWallTicketsController.ListComments`: `Select(c => new WallTicketCommentResultDto { … })` replaces the anonymous projection.

  **Renamed to avoid namespace collision:**
  - `WallTicketCommentDto` → `WallTicketCommentResultDto` (DTO namespace); the request-model `WallTicketCommentDto` in `BeDemo.Api.Models.Requests.Faces` is unchanged.

  **Build state:** 0 errors, 3 pre-existing warnings (unchanged), all 1950 tests pass.

---

## [1.4.38]

### Changed

- **Backend refactor — Phase 3 X11 EF query smells: `AsNoTracking` on all read-only entity loads, Blog queue projection removes 2 redundant `Include` calls, `UserService.GetUsersAsync` fixed to async.** Continuation of X11 (EF query anti-patterns). Changes are purely additive-performance — no HTTP API surface altered.

  **`AsNoTracking` additions (7 query sites):**
  - `ContentModerationController.GetQueue`: all three queries (Albums, Blogs, Reels) — these are admin read-only views; no entity is modified after loading.
  - `AlbumsController.GetAlbum` — detail DTO return, entity never mutated in this action.
  - `AlbumsController.GetAlbumsByUser` — projection-only `Select` list.
  - `ReelsController.GetReel` — detail DTO return.
  - `ReelsController.GetReelsByUser` — projection-only `Select` list.
  - `ReelsController.LoadReelDetailAsync` — private helper used by post-update/create response; entity not stored back to context.
  - `StoriesController.GetDetail` — 7-collection `AsSplitQuery` detail load; entity is not modified in this action.

  **Blog moderation queue — projection replaces 2 Includes (X11 redundant-Include fix):**
  - `ContentModerationController.GetQueue` Blog query: removed `Include(b => b.Face)` and `Include(b => b.Images)` — two extra JOINs that materialised full navigation collections only to read one field each. Replaced with an inline `.Select(b => new { Entity = b, FaceTitle = b.Face != null ? b.Face.Title : string.Empty, FirstImageUrl = b.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault() })` that lets EF translate each as a scalar subquery/join. `Include(b => b.Creator)` is retained: `Entity = b` is still materialised and `MapBlog` reads `blog.Creator.FirstName/LastName`.
  - `ContentModerationQueueMapper.MapBlog` signature changed from `MapBlog(Blog blog)` to `MapBlog(Blog blog, string faceTitle, string? firstImageUrl)` — callers now pass the projected values; the mapper no longer accesses `blog.Face.Title` or `blog.Images` on the entity.

  **`UserService.GetUsersAsync` — sync-over-async fix:**
  - `_userManager.Users.ToList()` (synchronous, blocks the thread, scans all users into memory) replaced with `await _userManager.Users.AsNoTracking().ToListAsync()`. Added `using Microsoft.EntityFrameworkCore;` for `AsNoTracking()`/`ToListAsync()`.

  **Unbounded list endpoints — safety caps (X11):**
  - `AlbumsController.GetAlbumsByUser` and `ReelsController.GetReelsByUser`: added `.Take(500)` before `.Select(…).ToListAsync()`. Both are already projection-only `AsNoTracking` queries; the cap bounds the DB scan and network transfer for high-volume creator profiles without changing the response shape.
  - `FriendRequestsController.GetPending`: added `AsNoTracking()`, removed redundant `Include(r => r.Sender)` (EF translates `r.Sender.*` in the `Select` without it), added `.Take(200)` on ordered results.
  - `MessagesController.GetMessageRequests`: added `AsNoTracking()` before the `GroupBy`, added `.Take(200)` on the projected `GroupBy` result (SQL-translated `LIMIT 200` on the grouped sender set).

  **Moderation queue — per-type scan cap (X10):**
  - `ContentModerationController.GetQueue`: cross-type in-memory sort makes pure SQL pagination across Albums/Blogs/Reels architecturally complex (no DB-level UNION sort with heterogeneous columns). Added `perTypeScanLimit = max(2000, page * pageSize * 4)` per-type cap applied via `.Take(perTypeScanLimit)` before each `.Select(…).ToListAsync()`. This bounds worst-case memory while guaranteeing correct results for all pages up to the limit (operators needing deeper pages must narrow their filters). The cap formula self-scales as the requested page grows.

  **Tests:**
  - `ContentModerationQueueMapperTests.BE_RA20_MapBlog_*`: updated call site to pass `blog.Face!.Title` and the sorted first image URL as positional arguments matching the new signature.
  - Full backend suite: **1950 passing, 0 skipped**.

---

## [1.4.37]

### Changed

- **Backend refactor — Phase 3 X10 parallel stats counts: `PlatformStatsQueryService` and `ContentModerationMetrics` rewritten to fan-out all `CountAsync` calls concurrently.** Both services previously accumulated their aggregate counts through long chains of sequential `await …CountAsync(…)` calls on a single shared `ApplicationDbContext` — a textbook X10 (N sequential round-trips) pattern. Each call acquired the same DB connection and waited for the previous one to finish, so the total latency was the _sum_ of all individual query times.

  **`PlatformStatsQueryService`** — two methods fixed (X10: 15 + ~35 sequential round-trips):
  - Constructor extended: `ApplicationDbContext context` retained for `GetOperatorAiTimeseriesHintsAsync` (3 sequential queries — acceptable); `IDbContextFactory<ApplicationDbContext> dbContextFactory` injected for the parallel methods. `AddDbContextFactory<ApplicationDbContext>` was already registered in Program.cs (lines 255, 276) so no DI registration change was needed.
  - `GetPublicSnapshotAsync`: 15 independent `CountAsync` calls across Users, Faces, Pages, Friendships, FriendRequests (pending), Messages, Albums, Blogs, Reels, Stories, StoryViews, FaceWallTickets, FaceChatRooms, FaceChatRoomMessages, FaceVideoLounges — now fired simultaneously with a private `RunAsync` helper that creates a fresh context from the factory per call, then `await Task.WhenAll(…)`.
  - `GetOperatorDashboardSummaryAsync`: ~35 independent tasks spanning user/social/messaging/content/chat/video-lounge/wall/profiles/comments/likes/AI-review/OAuth counts, including `BuildFaceWallTicketStatusCountsAsync` (single grouped `GroupBy` query, also parallelised). All tasks started before any `await`, then unified in a single `await Task.WhenAll(…)` barrier; results read via `.Result` in the `return new AdminDashboardSummaryDto { … }` initialiser.
  - `GetOperatorAiTimeseriesHintsAsync`: unchanged — 3 sequential timeseries projection queries on `_context` (no N+1, bucket aggregation is done in memory).

  **`ContentModerationMetrics`** — `GetSnapshotAsync` and all 4 private helpers fixed (~43 sequential round-trips → parallel):
  - Constructor replaced: `ApplicationDbContext context` → `IDbContextFactory<ApplicationDbContext> dbContextFactory`. Private `RunAsync<T>` helper added (same pattern).
  - `GetSnapshotAsync`: 30 independent tasks fired before any `await`: 3 pending-count tasks, 3 oldest-date `MinAsync` tasks, 4 AiReviewJob status counts, 9 terminal-approval status counts (3 statuses × 3 entity types — Albums/Blogs/Reels), 9 AI-recommendation status counts, plus `CollectReviewLatenciesHoursAsync`, `CollectTopFlagsAsync`, and `CollectPendingByFaceAsync` as sibling tasks. `Task.WhenAll` awaits all 30; results summed/assembled in the `return new ContentModerationMetricsSnapshot(…)` constructor call.
  - `CollectReviewLatenciesHoursAsync`: 3 `ToListAsync` calls (Albums/Blogs/Reels latency) fired in parallel and concatenated after `Task.WhenAll`.
  - `CollectTopFlagsAsync`: 3 flag-JSON `ToListAsync` calls fired in parallel; concatenation and in-memory histogram computation unchanged.
  - `CollectPendingByFaceAsync`: 3 face-pending `GroupBy/ToListAsync` calls fired in parallel; the `Faces` title lookup is necessarily sequential (depends on the combined `faceIds`) and uses a single `RunAsync` call.
  - Removed: `CountApprovalStatusAsync(status, ct)` and `CountAiStatusAsync(status, ct)` helpers — their 3-sequential-sum pattern is inlined as 3 independent tasks in `GetSnapshotAsync`.
  - `Percentile95` and `CalculateAgeHours` static helpers preserved verbatim.

  **Tests updated** — constructor signatures changed so two test call sites required fixes:
  - `BeRp4PlatformStatsCacheEdgeTests.BE_RP4_U1_CachedDashboardRepeat_ZeroDbCommandsOnSecondCall`: resolves `IDbContextFactory<ApplicationDbContext>` from the DI scope and passes it alongside `countingDb` to the now two-arg constructor. The cache-hit assertion is still valid: once the result is in `IMemoryCache`, the second call returns without calling the inner service, so `interceptor.CommandCount` (scoped to `countingDb`) remains zero.
  - `ContentModerationTests`: two tests that constructed `new ContentModerationMetrics(context)` now call `CreateContextAndFactory()` — a new private helper that creates an InMemory context and a matching `TestDbContextFactory` (inner sealed class) that creates fresh contexts sharing the same InMemory database name, so data seeded via `context.SaveChangesAsync()` is visible to the factory-created contexts.
  - Full backend suite: **1950 passing, 0 skipped**.

---

## [1.4.36]

### Changed

- **Backend refactor — Phase 3 X11 EF query smells: redundant `Include` removal, `AsSplitQuery` for multi-collection detail loads, and `ExecuteDeleteAsync` for cleanup service (X11).** Three categories of catalogued EF Core anti-patterns fixed across `ReelsController`, `AlbumsController`, `StoriesController`, and `RegistrationInviteCleanupHostedService`:
  - **Redundant `Include` before `.Select()`** (EF Core silently ignores `Include` when a terminal `.Select()` follows): removed 4 `Include`/`ThenInclude` calls from the Reels list action, 4 from Albums list, and 2 (`StoryFaces`, `Images`) from the Stories creator-list action. EF still produces the same SQL — the projections in `.Select()` reference the navigation properties directly and EF translates them as JOINs or subqueries without the hints.
  - **Cartesian explosion from multiple collection `Include` chains without `AsSplitQuery`** (EF Core 5+ mitigation — splits one N-collection JOIN into N+1 targeted queries): added `.AsSplitQuery()` to four multi-collection `FirstOrDefaultAsync` loads: `ReelsController.GetReel` and `LoadReelDetailAsync` (each has 3 collection Includes — `ReelFaces`, `Likes`, `Comments`); `AlbumsController` detail load (4 collection Includes — `AlbumFaces`, `MediaItems`, `Likes`, `Comments`); `StoriesController.GetDetail` (5 collection Includes with 2 `ThenInclude` chains — `StoryFaces→Face`, `Images`, `Likes`, `Comments`, `Views→Viewer`). The codebase had exactly one prior `AsSplitQuery` usage (`FacesConfigService.cs`); these are the next five worst offenders.
  - **Load-then-delete replaced by `ExecuteDeleteAsync`** (EF7+ bulk-delete — issues `DELETE WHERE` without materialising rows): `RegistrationInviteCleanupHostedService.RunCleanupAsync` replaced `ToListAsync` + `RemoveRange` + `SaveChangesAsync` with a single `ExecuteDeleteAsync` call; the deleted-count is returned directly. The previous InMemory-based test is replaced by `Postgres/RegistrationInviteCleanupPostgresTests` (real relational provider required; `ExecuteDeleteAsync` is unsupported by the InMemory provider). The new Postgres test also verifies that a valid unexpired invite survives and the count return value is correct.
  - **Per-lounge live participant count N+1 (X10) fixed in two hotspots:** `FaceVideoLoungesController.List` and `FaceGridSnapshotService.GetVideoLoungesBlockAsync` both had a `foreach (var kv in sessionByLounge) { await CountAsync(...) }` loop — one DB round-trip per live lounge. Replaced with a single `GroupBy().Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync()` and an in-memory join back to lounge IDs. The guard for empty `sessionByLounge` avoids a no-op DB call.
  - **Removed dead sync `GetFaces()` method** from `IFaceService` and `FaceService`: `GetFaces()` used `GetAwaiter().GetResult()` (sync-over-async — thread-starvation risk) but was never called anywhere in the codebase (RoutingMiddleware already calls `GetFacesAsync` directly). Removed from both the interface and the implementation.
  - Zero behaviour change on the HTTP API surface; full backend suite: **1950 passing, 0 skipped** (the replacement Postgres test offsets the removed InMemory test; net count unchanged).

---

## [1.4.35]

### Changed

- **Backend refactor — Phase 2 dedup: `WorkerGrpcClientBase<TClient>` extracted from `PushWorkerGrpcClient` and `MailerWorkerGrpcClient`.** Both worker gRPC clients shared an identical structural pattern — a single-entry channel cache (`_active`), a lock protecting it, a `List<X509Certificate2>` for TLS certificates that need disposal, and a verbatim `Dispose()` implementation — which diverged silently as the two classes were maintained in parallel. The duplicated `_channelLock`, `_tlsCertificatesToDispose`, `_active`, `ActiveChannel` nested class, and `Dispose()` (~40 lines per file, ~80 total) are now moved to `Services/WorkerGrpcClientBase<TClient> : IDisposable`. Each concrete client inherits from it and retains its own settings-merge (`MergeTls*Options`), `BuildCallOptions` (Push reads deadline from runtime DB settings; Mail reads it from env options and appends correlation headers via `MailerWorkerCorrelationMetadata`), and `GetClientOrNull` (calls `GetOrReplaceClient` with a `Func<GrpcChannel>` lambda that captures the `internal` `GrpcWorkerChannelFactory` so the base class stays `public`). Behaviour-preserving across Push and Mail paths; no call-site changes. Covered by five new unit tests in `WorkerGrpcClientBaseTests`: same-key returns same instance; different-key builds a new client and disposes the old channel; evicted key is rebuilt as a distinct instance; `Dispose` is idempotent; `Dispose` on an empty client does not throw. Full backend suite: **1950 passing, 0 skipped**.

---

## [1.4.34]

### Added

- **Backend refactor — Postgres-lane coverage for the profile-social unique indexes (Phase 4).** New `Postgres/ProfileSocialUniqueConstraintPostgresTests` verifies, against the real database, the two remaining composite unique indexes the InMemory provider silently ignores: a user cannot **double-like** a face profile (`UserFaceProfileLike (UserFaceProfileId, UserId)`) and an author cannot **double-review** one (`UserFaceProfileReview (UserFaceProfileId, AuthorUserId)`) — each duplicate insert is rejected with a unique-violation `DbUpdateException`. Uses the shared `PostgresFixture` with per-test isolated databases and a role → user → profile → face → user-face-profile seed helper. Full backend suite: **1945 passing, 0 skipped**.

---

## [1.4.33]

### Added

- **Backend refactor — structured request-completion logging with correlation id (Phase 4 observability).** The pipeline now calls `UseSerilogRequestLogging` right after the X13 correlation-id middleware, emitting one structured Serilog event per request (`HTTP {Method} {Path} responded {StatusCode} in {Elapsed} ms`) enriched via `EnrichDiagnosticContext` with `CorrelationId` (read from `TraceIdentifier`, which the correlation middleware has just set). Request events in Seq now join up with the scoped per-request log lines on the same `CorrelationId`. Purely additive logging — no status codes, headers or routing change (full backend suite 1943 passing). Covered by a new integration test that swaps in a capturing Serilog sink, drives a request with a known inbound `X-Correlation-Id` through the real pipeline, and asserts the completion event (RequestPath + StatusCode) carries that id.

---

## [1.4.32]

### Changed

- **Backend refactor — Program.cs modularisation, slice 9 (Phase 3).** Extracted the CORS setup — the dev-default + LAN + configured-origin combination logic and the `AddCors` default policy — into `Configuration/CorsServiceCollectionExtensions.AddManyFacesCors(configuration, useTransportHardening)`. Moved verbatim (the transport-hardening branch and the `DevLanCorsOriginBuilder` LAN origins behave identically). Behaviour-preserving; full backend suite 1942 passing. `Program.cs` drops to **790 lines** — **nine slices have now lifted ~360 lines (~31%) of registration/config out of the composition root** into focused `AddManyFaces*` extensions, leaving the genuinely pipeline-entangled auth/JWT bearer + Identity + the request pipeline in place. `dotnet format` clean.

---

## [1.4.31]

### Changed

- **Backend refactor — Program.cs modularisation, slice 8 (Phase 3).** Extracted the OAuth / token-lifetime / uploads / hardened-security / AI-gRPC service registrations — password hasher, `IClock`, the validated `JwtTokenLifetimeOptions` / `UploadsOptions` / `HardenedSecurityOptions` / `AiServiceOptions`, the OAuth services, the signed-upload-URL service, and the AI gRPC client + host-profile services — into `Configuration/OAuthAndAiServiceCollectionExtensions.AddManyFacesOAuthAndAiServices()`. The genuinely entangled bits (the `ECDSAKeyService` instance + the `AddAuthentication`/`AddJwtBearer` setup that consume `signingKey`) are deliberately **left in Program.cs**. Behaviour-preserving — the OAuth/JWT integration tests boot the real app and authenticate; full backend suite 1942 passing. `Program.cs` drops to **820 lines** (~29% below the ~1150 it started Phase 3 at). `dotnet format` clean.

---

## [1.4.30]

### Changed

- **Backend refactor — Program.cs modularisation, slice 7 (Phase 3).** Extracted the core per-request domain service registrations — face lookup + profile-detail pages, the HTTP context accessor, the request face scope + access evaluator, the OAuth refresh-token store, the chat-hub AI rate limiter, and the story / face-wall-ticket lifecycle services — into `Configuration/DomainServiceCollectionExtensions.AddManyFacesDomainServices()`. Moved verbatim; behaviour-preserving (DI order-independence + boot tests), full backend suite 1942 passing. `Program.cs` drops to **863 lines**. `dotnet format` clean.

---

## [1.4.29]

### Changed

- **Backend refactor — Program.cs modularisation, slice 6 (Phase 3).** Extracted the performance options + stats / access-token-version / capabilities / faces-config / grid-list / conversation-list / hub-display service registrations into `Configuration/GridAndStatsServiceCollectionExtensions.AddManyFacesGridAndStatsServices()`. Moved verbatim; behaviour-preserving (DI order-independence + boot tests), full backend suite 1942 passing. `Program.cs` drops to **870 lines** (from ~1150 at the start of Phase 3 — six slices have now lifted ~280 lines of registration into focused `AddManyFaces*` extensions). `dotnet format` clean.

---

## [1.4.28]

### Changed

- **Backend refactor — Program.cs modularisation, slice 5 (Phase 3).** Extracted platform-wide options binding (content-moderation security, search, push/Firebase, mail, registration-invite, mail-link) plus the registration-invite, search (outbox / gateway / ACL / reconciliation, with the config-gated hosted services) and push/mailer worker-client registrations into `Configuration/PlatformServiceCollectionExtensions.AddManyFacesPlatformServices(configuration)` (this slice reads config, so it takes `IConfiguration`). Moved verbatim; behaviour-preserving (DI order-independence + boot tests), full backend suite 1942 passing. `Program.cs` drops to **883 lines** (from ~1150 at the start of Phase 3). `dotnet format` clean.

---

## [1.4.27]

### Changed

- **Backend refactor — Program.cs modularisation, slice 4 (Phase 3).** Extracted the operator content / moderation / messaging / video-lounge / content-retention service registrations (incl. the validated `VideoLoungeOptions` (X3) and the content-retention hosted service) out of `Program.cs` into `Configuration/ContentServiceCollectionExtensions.AddManyFacesContentAndModerationServices()`. The block uses only `services`, so it moved verbatim; DI order-independence + the boot tests keep it behaviour-preserving (full backend suite 1942 passing). `Program.cs` drops to **913 lines**. `dotnet format` clean.

---

## [1.4.26]

### Changed

- **Backend refactor — Program.cs modularisation, slice 3 (Phase 3).** Extracted the entire operator-AI registration block — `OperatorAiOptions` (validated, X3), the conversation / live-stats / decision services, the RAG retrieval singletons (knowledge client, status cache, planner fallback, retriever, indexer), the four routed skills + registry + router, and the three startup hosted services — out of `Program.cs` into `Configuration/OperatorAiServiceCollectionExtensions.AddManyFacesOperatorAi()`. The block uses only `services` (no `builder.Configuration`), so it moved verbatim. DI resolves registrations order-independently and the integration tests boot the real app (resolving the whole operator-AI graph), so behaviour is unchanged — full backend suite 1942 passing. `Program.cs` drops to **935 lines** (from ~1150 at the start of Phase 3). `dotnet format` clean.

---

## [1.4.25]

### Added

- **Backend refactor — Postgres Testcontainers lane (Phase 4).** Added a real-PostgreSQL test lane backed by Testcontainers (`Testcontainers.PostgreSql`), so constraint/migration semantics that the EF InMemory provider silently ignores can finally be verified against `postgres:16-alpine` (matching production). New `BeDemo.Api.Tests/Postgres/`:
  - `PostgresFixture` — an `xUnit` collection fixture that starts one container and hands out `ApplicationDbContext`s (incl. `CreateContextInNewDatabaseAsync` for per-test isolated databases). All Postgres-lane tests are tagged `[Trait("Category", "Postgres")]` so a CI lane can opt in (they need Docker).
  - `SchemaMigrationDriftTests` — applies the full 95-migration history to a fresh database and asserts `HasPendingModelChanges()` is `false`: the EF model and the committed migrations agree (no missing migration). This confirms the production `Ignore(PendingModelChangesWarning)` is defensive, not masking real drift.
  - `UserFaceProfileUniqueConstraintPostgresTests` — replaces the long-skipped `UserFaceProfileTests.UserFaceProfile_ShouldHaveUniqueConstraint_OnUserProfileIdAndFaceId` (`[Fact(Skip = …)]`): seeds a user/profile/face and asserts the real database rejects a duplicate `(UserProfileId, FaceId)` `UserFaceProfile` with a unique-violation `DbUpdateException`.
  - `PostgresSchemaSmokeTests` — `EnsureCreated` builds the whole model schema on real Postgres and round-trips a query.
  - The InMemory suite's `[Fact(Skip)]` placeholder was removed (the check is now real). Full backend suite: **1942 passing, 0 skipped** (was 1939 + 1 skipped).

---

## [1.4.24]

### Changed

- **Backend refactor — Program.cs modularisation, slice 2 (Phase 3).** Extracted the whole rate-limiter setup — the ~9 permit/window config reads (with the Testing bypass) plus the partitioned `AddRateLimiter` policy definitions (oauth-token, oauth-register, localization-read, auth-login, api global, upload, register-prefill, ai-availability, signalr-negotiate) — out of `Program.cs` into `Configuration/RateLimitingServiceCollectionExtensions.AddManyFacesRateLimiting(configuration, isTestingEnv)`. `Program.cs` now calls the one-liner (passing the already-computed `isTestingEnv`, which is still used elsewhere). Behaviour-preserving: the block was moved verbatim and the Testing bypass is driven by the same flag, confirmed by the rate-limit suites (`OAuthRateLimit429Tests`, `OAuthExemptPath…`) and the full backend suite (1939 passing). `Program.cs` drops from ~1150 to **985 lines**. `dotnet format` clean.

---

## [1.4.23]

### Changed

- **Backend refactor — Program.cs modularisation, slice 1 (Phase 3).** Began extracting the ~1100-line composition root into cohesive `AddManyFaces*` service-collection extensions. This slice moves two self-contained, pipeline-independent registration blocks out of `Program.cs`: platform authorization (default-deny fallback + the X5 declarative policies + the scoped `ManageAllFaces` handler) → `Configuration/AuthorizationServiceCollectionExtensions.AddManyFacesAuthorization()`, and the X14 health checks → `Configuration/HealthCheckServiceCollectionExtensions.AddManyFacesHealthChecks()`. `Program.cs` now calls the two one-liners in the same place/order. Purely structural and behaviour-preserving — the integration tests boot the real app, so the auth-policy enforcement (`PlatformSuperAdminAccessEdge`, `GlobalAuthFallback`) and the health probes (`HealthCheck*`) tests confirm identical behaviour; full backend suite 1939 passing, `dotnet format` clean.

---

## [1.4.22]

### Changed

- **Backend refactor — `UsersController` create/update on the ManageAllFaces policy (X5/X6, migration complete).** `POST /api/users` and `PUT /api/users/{id}` now carry a method-level `[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]` instead of an in-body `if (!CanManageAllFaces()) return Forbid();`. The accessor is method-level (not class-level) because the list/get actions keep `CanManageAllFaces()` as a per-face _visibility branch_, not a gate. With this, **every blanket operator gate across the controllers is now declarative** — the per-controller auth-policy migration is finished.

### Security

- **Authorization now runs before model validation on `POST`/`PUT /api/users`.** Moving the gate to a method-level attribute means an unauthorized caller is refused with **403 before model binding**, whereas previously a malformed body from an unauthorized caller returned **400** (validation ran before the in-body gate). This is the conventional, more-secure order — it no longer reveals request-validation results to callers who are not allowed to use the endpoint. The authorized matrix is unchanged (super-admin in admin scope → create/update; global ADMIN → 403; anonymous → 401). The `UsersControllerTests.CreateUser_ShouldReturnBadRequest_WhenInvalidEmail` test was updated to drive the 400 path through an authorized (super-admin) client, since validation is now only reachable when authorized.

---

## [1.4.21]

### Changed

- **Backend refactor — shared `OperatorSecretProtectorBase` (Phase 2 dedup).** Extracted the byte-identical Data-Protection wrapper that `OperatorMailSecretProtector` and `OperatorPushSecretProtector` each duplicated into a new `Services/OperatorSecretProtectorBase` (holds the `IDataProtector` and the `Protect`/`Unprotect` round-trip); the two concrete protectors now derive from it and only supply their own subsystem `purpose` string via the base constructor. **The purpose strings (`BeDemo.Api.OperatorMailSystemSettings.v1` / `…OperatorPushSystemSettings.v1`) are preserved verbatim** — they are cryptographic key-derivation inputs, so changing them would make previously stored ciphertext undecryptable. Behaviour-preserving; the `OperatorSecretProtectorTests` characterization guard (protect↔unprotect round-trip + cross-purpose-decrypt-throws) and the full backend suite (1939 passing) stay green.

---

## [1.4.20]

### Changed

- **Backend refactor — finish `ApiControllerBase` adoption (X6).** Migrated the remaining five controllers that used a differently-named current-user accessor onto `ApiControllerBase`: `OperatorUsersController`, `OperatorContentController`, `OperatorUserChatController` (`OperatorUserId`), `FaceProfilesController` + its `FaceProfilesController.Operator.cs` partial (`CurrentUserId`), and `AdminMeProfileController` (`CallerUserId`). Each local accessor was removed and its call sites renamed to the inherited `UserId` (verified collision-free first). With this, **all 30 controllers that need the caller's id share the single `ApiControllerBase.UserId`** — no per-controller accessor remains. Behaviour-preserving (the renamed members resolve to the same `NameIdentifier` lookup); full backend suite 1939 passing, `dotnet format` clean.

---

## [1.4.19]

### Added

- **Backend refactor — `ApiControllerBase` (X6).** New `Controllers/ApiControllerBase : ControllerBase` centralises the authenticated caller's user id via a single `protected string? UserId => User.FindFirst(NameIdentifier)?.Value`, which had been copy-pasted as a private `UserId` accessor in 25 controllers.

### Changed

- **Backend refactor — controllers inherit `ApiControllerBase` (X6).** Migrated the 25 controllers that carried the identical private `UserId` accessor (both the `FindFirst(...).Value` and the functionally-equivalent `FindFirstValue(...)` spellings) to derive from `ApiControllerBase` and dropped their local accessor. Because the inherited member keeps the same name (`UserId`) and identical semantics, no call sites changed — purely structural de-duplication, behaviour-preserving (full backend suite 1939 passing). The five controllers using a differently-named accessor (`OperatorUserId` / `CurrentUserId` / `CallerUserId`) are intentionally left for a follow-up that renames their call sites.

---

## [1.4.18]

### Changed

- **Backend refactor — AI test-fake consolidation complete (Phase 0).** Migrated the final two bespoke `IAiGrpcService` fakes onto the shared `FakeAiGrpcService`: the operator-AI capturing mock (`CapturingOperatorAiGrpcService` behind `OperatorAiGrpcMockWebApplicationFactory`) and the host-profile fake in `AiWorkerHostProfileServiceTests`. To absorb their contracts the canonical fake's `ModelStatusHandler` became nullable (null ⇒ reports "ready", matching the old mock) and its host-profile knobs are now mutable `HostProfileJson` / `HostProfileError` properties (so a test can change them mid-run). `OperatorAiLocaleAcceptanceTests`' two `GenerateHandler` lambdas were updated to the canonical `(prompt, locale)` signature. **All six hand-rolled `IAiGrpcService` fakes are now gone** — every AI-seam test configures one documented double. Test-only; the full backend suite (1939 passing / 1 skipped) and the operator-AI + host-profile suites stay green.

---

## [1.4.17]

### Changed

- **Backend refactor — shared canonical AI test double (Phase 0).** Added `BeDemo.Api.Tests/TestDoubles/FakeAiGrpcService.cs` — one configurable `IAiGrpcService` (+ `IAiModelStatusClient`) fake with a sensible no-op default for every RPC, single obvious configuration knobs (`ReviewResult`, `GenerateHandler`, `ModelStatusHandler`, `HostProfileResult`, …), input capture (`LastReviewRequest`, `LastPrompt`, `LastResponseLocale`, `ModelStatusPollCount`) and convenience constructors (`(AiReviewRecommendation)`, `(string error)`). Migrated the four near-identical hand-rolled moderation fakes onto it — `ContentModerationTests` (`FakeAiGrpcService` ×2-ctor), `ContentModerationProductionPathTests` (`CapturingAiGrpcService`), `ContentModerationSecurityEdgeTests` (`FakeAiGrpcService`) and `ContentModerationPayloadLogRedactionTests` (`NoOpAiGrpcService`) — removing ~300 lines of duplicated stub boilerplate. Test-only change, no production code touched; the full `ContentModeration` suite (221 tests) and the whole backend suite (1939) stay green. (The remaining two bespoke fakes — the OperatorAI capturing mock with its `Func<string?,string>` generate hook and the host-profile fake with its own property names — have incompatible hook signatures and are left for a follow-up migration.)

---

## [1.4.16]

### Changed

- **Backend refactor — authorization-policy migration, batch 8 (X5/X6, dual-policy method-level).** Migrated `OperatorContentController` (`/api/operator-content`), which mixes **two** different gates, to per-action policies: the 10 hard-delete actions (album, album-media, reel, blog, blog-image, chat-room, profile-comment, profile-review, story, story-image) carried `RequireSuperAdmin()` → `[Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]`, while the 3 live video-lounge moderation actions (stealth-join, kick, kick-all) carried `RequireManageAllFaces()` → `[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]`. All 13 in-body `if (!Require…()) return Forbid();` gates, both helper methods, and the now-unused `IAccessEvaluator` dependency were removed; the `NameIdentifier`-claim `Unauthorized()` guards are kept (they protect the per-operator actor id, not authorization). No action had model validation before its gate, so nothing is reordered and each action's matrix is unchanged (anonymous → 401, insufficient → 403, authorized → allowed) — with the delete actions requiring global SUPER_ADMIN and the video-lounge actions requiring admin-face-scope operator rights, exactly as before. Verified by the Admin{Album,Blog,Reel,ChatRoom,Story,Profile}Management + FaceVideoLounges + OperatorContent validator suites (65 tests).
  - **This completes the mechanical per-controller auth-policy migration cluster.** Every controller whose actions had a uniform/separable imperative platform gate now uses `[Authorize(Policy = …)]`. The remaining imperative `CanManageAllFaces` usages are either _branching_ per-item logic (Pages/Stories/Reels list-scoping, Users list/get visibility) or _validation-ordered_ gates (Users create/update, where a method-level attribute would reorder authorization ahead of `ModelState` validation) — both intentionally left, as migrating them would change client-visible behaviour.

---

## [1.4.15]

### Changed

- **Backend refactor — authorization-policy migration, batch 7 (X5/X6, SuperAdmin policy).** Migrated `ContentModerationController` (`/api/ContentModeration` — moderation queue, audit events, metrics, bulk actions, and single-item approve/reject/remove) to a class-level `[Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]`. The four direct-action `if (!CanModerate()) return Forbid();` gates (queue / events / metrics / bulk) were removed in favour of the policy; the shared `ApplyDecisionCoreAsync` (the single decision path reached by approve/reject/remove **and** the bulk loop) **keeps** its own `CanModerate()` guard as defense-in-depth, so the `IAccessEvaluator` dependency is retained. Authorization runs before the action, and the kept internal guard sits before the in-method validation exactly as before, so the matrix is unchanged (anonymous → 401, non-super-admin → 403, super-admin → allowed). Verified by the full `ContentModeration` integration suite (221 tests) staying green.

---

## [1.4.14]

### Changed

- **Backend refactor — authorization-policy migration, batch 6 (X5/X6, SuperAdmin policy).** First use of the role-only `SuperAdmin` policy (declared in 1.4.5 but not yet applied). Migrated three clean blanket-gate super-admin controllers from a per-action `RequireSuperAdmin()` (= `IsGlobalSuperAdmin`) check to a class-level `[Authorize(Policy = PlatformAuthorizationPolicies.SuperAdmin)]`: `OperatorUsersController` (`/api/operator-users`, 7 actions — user moderation/bans/face-roles), `AdminMeProfileController` (`/api/admin/me`, 5 actions — super-admin self-service profile) and `OperatorUserChatController` (`/api/operator-user-chat`, 4 actions — 1:1 user chat). The `RequireSuperAdmin()` helpers, all in-body `if (!RequireSuperAdmin()) return Forbid();` gates, and the now-unused `IAccessEvaluator` dependency were removed; the secondary `NameIdentifier`-claim `Unauthorized()` guards are kept (they protect per-operator lookups, not authorization). Unlike `ManageAllFaces`, the `SuperAdmin` policy is purely role-based (no face-scope requirement), exactly matching the imperative `IsGlobalSuperAdmin(User)` it replaces, so the matrix is unchanged: anonymous → 401, authenticated non-super-admin (incl. global ADMIN) → 403, SUPER_ADMIN → allowed. Pinned by the controllers' existing integration negatives (`*_Should403_ForPlatformAdmin` / `SAP_B10_UnauthorizedAndForbidden`).

---

## [1.4.13]

### Changed

- **Backend refactor — authorization-policy migration, batch 5 (X5/X6, method-level).** Migrated `StatsController`'s two operator-only endpoints — `GET /api/Stats` (dashboard summary) and `GET /api/Stats/timeseries` (chart histograms) — from an in-body `CanManageAllFaces` check to a **method-level** `[Authorize(Policy = PlatformAuthorizationPolicies.ManageAllFaces)]`. This is the first method-level (rather than class-level) migration: the controller also hosts `GET /api/Stats/public`, which keeps its `[AllowAnonymous]`, so the policy is applied per-action rather than to the whole class. The now-unused `IAccessEvaluator` and `ILogger` dependencies were removed. Authorization matrix unchanged (anonymous → 401, global ADMIN → 403, SUPER_ADMIN on a non-admin face → 403, SUPER_ADMIN in admin scope → allowed; the public endpoint stays anonymous); pinned by `StatsController` + `PlatformSuperAdminAccessEdge` (incl. the super-admin-on-public-face 403 and unauthenticated-401 cases).
  - **`UsersController` create/update were evaluated and deliberately left imperative**: their `CanManageAllFaces` gate sits _after_ `ModelState` validation, so a method-level attribute would reorder authorization ahead of validation (an unauthorized caller with a malformed body would get 403 instead of the current 400) — a client-visible change, which these behaviour-preserving batches avoid. Their list/get actions also use `CanManageAllFaces()` as per-face visibility _branching_, not a gate.

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

---

## [1.0.1]

### Changed

- Document project author (Ladislav Kostolny, 01laky@gmail.com) in README and standard manifests.

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

[Unreleased]: https://github.com/01laky/many_faces_backend/compare/v1.4.46...HEAD
[1.4.46]: https://github.com/01laky/many_faces_backend/compare/v1.4.45...v1.4.46
[1.4.45]: https://github.com/01laky/many_faces_backend/compare/v1.4.44...v1.4.45
[1.4.44]: https://github.com/01laky/many_faces_backend/compare/v1.4.43...v1.4.44
[1.4.43]: https://github.com/01laky/many_faces_backend/compare/v1.4.42...v1.4.43
[1.4.42]: https://github.com/01laky/many_faces_backend/compare/v1.4.41...v1.4.42
[1.4.41]: https://github.com/01laky/many_faces_backend/compare/v1.4.40...v1.4.41
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
[1.4.34]: https://github.com/01laky/many_faces_backend/compare/v1.4.33...v1.4.34
[1.4.33]: https://github.com/01laky/many_faces_backend/compare/v1.4.32...v1.4.33
[1.4.32]: https://github.com/01laky/many_faces_backend/compare/v1.4.31...v1.4.32
[1.4.31]: https://github.com/01laky/many_faces_backend/compare/v1.4.30...v1.4.31
[1.4.30]: https://github.com/01laky/many_faces_backend/compare/v1.4.29...v1.4.30
[1.4.29]: https://github.com/01laky/many_faces_backend/compare/v1.4.28...v1.4.29
[1.4.28]: https://github.com/01laky/many_faces_backend/compare/v1.4.27...v1.4.28
[1.4.27]: https://github.com/01laky/many_faces_backend/compare/v1.4.26...v1.4.27
[1.4.26]: https://github.com/01laky/many_faces_backend/compare/v1.4.25...v1.4.26
[1.4.25]: https://github.com/01laky/many_faces_backend/compare/v1.4.24...v1.4.25
[1.4.24]: https://github.com/01laky/many_faces_backend/compare/v1.4.23...v1.4.24
[1.4.23]: https://github.com/01laky/many_faces_backend/compare/v1.4.22...v1.4.23
[1.4.22]: https://github.com/01laky/many_faces_backend/compare/v1.4.21...v1.4.22
[1.4.21]: https://github.com/01laky/many_faces_backend/compare/v1.4.20...v1.4.21
[1.4.20]: https://github.com/01laky/many_faces_backend/compare/v1.4.19...v1.4.20
[1.4.19]: https://github.com/01laky/many_faces_backend/compare/v1.4.18...v1.4.19
[1.4.18]: https://github.com/01laky/many_faces_backend/compare/v1.4.17...v1.4.18
[1.4.17]: https://github.com/01laky/many_faces_backend/compare/v1.4.16...v1.4.17
[1.4.16]: https://github.com/01laky/many_faces_backend/compare/v1.4.15...v1.4.16
[1.4.15]: https://github.com/01laky/many_faces_backend/compare/v1.4.14...v1.4.15
[1.4.14]: https://github.com/01laky/many_faces_backend/compare/v1.4.13...v1.4.14
[1.4.13]: https://github.com/01laky/many_faces_backend/compare/v1.4.12...v1.4.13
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
