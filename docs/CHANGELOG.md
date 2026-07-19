# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Updated

- Updated NuGet packages.

## [1.3.0] - 2026-07-05

### Added

- **ADR-0027 Phase B — connection registry + auth-event connection terminator.**
  `InvocationConnectionRegistry` is the first implementation of `IInvocationConnectionRegistry`
  (`Cirreum.Contracts`): a per-server, `ConcurrentDictionary`-backed index of active long-lived
  connections, fed by a new framework-shipped `IConnectionLifecycle` (`ConnectionRegistryLifecycle`)
  through the connect/disconnect hooks the SignalR and WebSocket adapters already dispatch. Subjects
  are resolved at *query* time via `ClaimsHelper.ResolveId` (the same resolution behind `IUserState.Id`)
  over the connection's effective principal, so Two-Phase Auth promotions are honored with no
  re-registration and no staleness window.
- `ConnectionTerminationHandler` reacts to `CredentialRevoked`, `UserAccountDisabled`, and
  `SessionTerminationRequested` by calling the idempotent `IInvocationConnection.Abort()` on the
  subject's registered connections. `CredentialRevoked`/`UserAccountDisabled` abort all of the
  subject's connections (conservative posture); `SessionTerminationRequested.SessionId` scopes
  termination to the connection whose `ConnectionId` matches ("sign out this device" — the app reads
  the id off `IInvocationContext.Connection`) or whose effective principal carries an equal OIDC
  `sid` claim (browser-session scope). Scoping never widens beyond the subject; an unmatched scoped
  request terminates nothing. All registered in `AddCoreServices()`; the handlers sit inert until an
  `IAuthenticationEventPublisher` (ADR-0025) delivers events.
- **`CirreumUserIdProvider`** — aligns SignalR's `Clients.User(...)` addressing with the framework's
  subject identity (`ClaimsHelper.ResolveId` ≡ `IUserState.Id` ≡ auth-event `Subject`). SignalR's own
  default reads `ClaimTypes.NameIdentifier` only, which diverges from the framework's oid-preferring
  resolution on Entra-backed apps — two user-addressing systems silently disagreeing about who a
  connection belongs to. Wired by `TryAddSignalRInvocationFilter` only while SignalR's
  `DefaultUserIdProvider` is still in place; an app-registered custom `IUserIdProvider` always wins.
  Note: SignalR caches the user id at connection time (inherent to SignalR), so promoted connections
  remain addressed by their upgrade identity in `Clients.User(...)` — promotion-aware targeting goes
  through `IInvocationConnectionRegistry.FindBySubject` + `SendAsync` instead.
- First test suite for this repo (31 tests): registry, lifecycle registrar, terminator (including
  promotion, sid-scoping, and cross-subject isolation), user-id provider wiring, and DI registration
  coverage.

### Fixed

- **Connection-registry leak on the raw-WebSocket connect-failure path.** `ConnectionRegistryLifecycle`
  registered a connection in `OnConnectedAsync`, but the raw-WebSocket orchestrator skips
  `OnDisconnectedAsync` (the only unregister signal) when a connect is *rejected* (a later
  `IConnectionLifecycle` returns `false`) or *faults* (a hook or the app handler throws) — so every such
  connection leaked permanently into the singleton registry (unbounded growth under repeated rejected
  upgrades, plus phantom `FindBySubject`/`All()` results and stale `ClaimsPrincipal` retention). Cleanup is
  now also tied to the connection's own `Aborted` token: both failure paths dispose the connection, and
  `WebSocketConnection.DisposeAsync` cancels `Aborted` first, so `Unregister` runs on every teardown path
  for both transports. `OnDisconnectedAsync` still removes promptly on the graceful path; `Unregister` is
  idempotent. This follows `IConnectionLifecycle`'s intended division (now documented in
  `Cirreum.Contracts`): `OnDisconnectedAsync` observes a live connection's disconnect, while
  `IInvocationConnection.Aborted` is the cleanup signal that also covers rejected/faulted establishment.
  (Found by an adversarial multi-agent review of this change set — the sole surviving finding.)

- **Two-Phase Auth promotion is now actually consumed.** `TwoPhaseAuth.Promote`
  (`Cirreum.Runtime.AuthenticationProvider`) stamps the promoted principal into
  `IInvocationConnection.Items`, and both its docs and `AuthenticationContextKeys.PromotedPrincipal`'s
  docs promised the per-invocation `UserStateAccessor` reads it in preference to the connection's
  original principal — but nothing anywhere read the slot, so promoted connections kept flowing as
  anonymous. `SignalRInvocationContext` and `WebSocketInvocationContext` now construct their
  per-invocation `User` snapshot from the effective principal (promoted when present, upgrade-time
  otherwise), making the documented precedence true for `UserStateAccessor` and every other
  `invocation.User` consumer. Promotion takes effect from the next invocation on the connection.

## [1.2.8] - 2026-07-04

### Updated

- Updated NuGet packages.

## [1.2.7] - 2026-07-04

### Updated

- Updated NuGet packages.

## [1.2.6] - 2026-07-04

### Updated

- Updated NuGet packages.

## [1.2.5] - 2026-05-10

### Updated

- Updated NuGet packages.

## [1.2.4] - 2026-05-10

### Fixed

- **`UserStateAccessor` now double-writes the resolved `IApplicationUser` to both per-invocation `Items` AND per-connection `Connection.Items`** when the lazy-resolve path fires (cache miss → `IApplicationUserResolver` returns a non-null user). Future-proofs the framework for the AI/LLM act-on-behalf-of seam (Piece 2): when a null-scheme `IApplicationUserResolver` eventually registers and the lazy-resolve path goes live for header-auth or M2M-on-behalf-of-human shapes on long-lived connections, the resolved user automatically propagates to the connection-lifetime bag so subsequent invocations on the same connection seed correctly via the source adapter's per-invocation `Items` copy (`Cirreum.Invocation.SignalR` + `Cirreum.Invocation.WebSockets` patches). Without this propagation, every per-Hub-method or per-WebSocket-message invocation would re-invoke the resolver and hammer the underlying lookup (DB, IdP). Today the line is dead-code for all current resolver registrations (audience-auth pre-populates the cache at upgrade via the claims transformer; header-auth has no matching resolver and short-circuits before the write). The branch exists to prevent the surprise the day the AI/LLM Piece 2 work introduces a null-scheme resolver. Read paths in `UserStateAccessor` and any other consumer remain unchanged — they continue to read `invocation.Items` only. The connection-bag awareness is contained to the writer, on this single lazy-resolve path. The `null`-coalescing `is { } connection` check ensures HTTP invocations (where `Connection` is null) skip the write transparently.

## [1.2.3] - 2026-05-09

### Updated

- Updated NuGet packages.

## [1.2.2] - 2026-05-09

### Updated

- Updated NuGet packages.

## [1.2.1] - 2026-05-08

### Updated

- Updated NuGet packages.

## [1.2.0] - 2026-05-07

### Added

- **`InvocationContextHttpMiddleware`** — per-request middleware that materializes an `IInvocationContext` for the active `HttpContext` and publishes it through `IInvocationContextAccessor`. Snapshots `User` (immutable for the invocation), aliases `HttpContext.Items` (same dictionary reference — no copy), and exposes `RequestServices` / `RequestAborted` through the seam.
- **`UseInvocationContext()`** extension on `IApplicationBuilder` — registers the bridge middleware. Intended placement: after `UseAuthentication` / `UseAuthorization`, before endpoint execution. `Cirreum.Runtime.Server` will pick this up in its `Build()` composition.
- **`IInvocationContextAccessor`** registered in `AddCoreServices()` (singleton, `AsyncLocal`-backed; matches `IHttpContextAccessor` convention).

### Changed

- **`UserStateAccessor` (renamed from `UserAccessor`)** now reads identity, items, services, and authenticated scheme through `IInvocationContextAccessor.Current` instead of `IHttpContextAccessor.HttpContext` directly. The four-step enrichment pipeline (claims pre-enrich → `SetAuthenticatedPrincipal` → `ResolveApplicationUserAsync` → `ResolveAuthenticationBoundary`) is unchanged in shape and behavior; only the seam it consumes is refactored. Per-scheme `IApplicationUserResolver` dispatch and all `AuthenticationContextKeys` slot reads/writes continue to work transparently because `IInvocationContext.Items` IS `HttpContext.Items` (aliased, not copied) for HTTP-sourced invocations. The class is `internal sealed`; the rename has no public API impact.
- App-name header (`RemoteIdentityConstants.AppNameHeader`) is now snapshotted by `HttpInvocationContext` at middleware entry and exposed through an internal seam consumed by `UserStateAccessor` via feature-check cast. Eliminates the mid-pipeline `IHttpContextAccessor` re-read. Non-HTTP invocation sources don't satisfy the cast — result is `null`, which is correct since those sources have no HTTP headers.

### Migration from 1.1.0

For framework-internal consumers: no action. The `IUserStateAccessor` contract is unchanged and the enrichment pipeline produces the same `IUserState` for the same input. The implementation type was renamed from `UserAccessor` to `UserStateAccessor` for consistency with the interface name; this is an `internal sealed` class so the rename has no public API impact.

For host composition: apps using `Cirreum.Runtime.Server`'s `Build()` will pick up the new middleware automatically once Runtime.Server ships its corresponding update. Apps composing the pipeline manually need to add `app.UseInvocationContext()` after `UseAuthorization()`.

## [1.1.0] - 2026-05-01

Per-scheme application user resolution support, paired with the `Cirreum.Core
5.0.0` rework. `UserAccessor` now dispatches `IApplicationUserResolver` by the
request's authenticated scheme on the cache-miss fallback path, enabling
multi-IdP server hosts that register one resolver per scheme.

### Changed

- **`UserAccessor.ResolveApplicationUserAsync` cache-miss path now
  dispatches by scheme.** Previously it called
  `serviceProvider.GetService<IApplicationUserResolver>()` and invoked the
  single registered resolver. Now it calls
  `serviceProvider.GetServices<IApplicationUserResolver>()`, reads the
  request's authenticated scheme from
  `AuthenticationContextKeys.AuthenticatedScheme` (or
  `Identity.AuthenticationType` as a fallback), and selects the resolver
  whose `Scheme` matches — falling back to the resolver whose `Scheme` is
  `null` when no exact match is registered. No matching resolver is the
  correct outcome for operator/machine-track callers (workforce IdP,
  ApiKey, SignedRequest, External BYOID) — they have no application user
  record by design.
- **`UserAccessor.ResolveAuthenticationBoundary` and
  `ResolveApplicationUserAsync` updated** to read the canonical scheme from
  `AuthenticationContextKeys.AuthenticatedScheme` (replacing the removed
  `IAuthenticationBoundaryResolver.ResolvedSchemeKey` const). Application
  user cache reads/writes use `AuthenticationContextKeys.ApplicationUserCache`
  (replacing the removed `IApplicationUserResolver.CacheKey` const).
- **`UserAccessor.ResolveApplicationUserAsync` now always calls
  `SetResolvedApplicationUser`** (with `null` when no record was resolved)
  before returning. Previously, the cache-miss path skipped the call when
  the resolver was unregistered or returned `null`, leaving
  `IsApplicationUserLoaded` at `false` for those cases. Now
  `IsApplicationUserLoaded` flips to `true` after every resolution attempt
  regardless of outcome, distinguishing "attempted, no record" from
  "not yet attempted." This aligns server-side semantics with the WASM
  `IInitializationOrchestrator` Phase 1 behavior.
- **`UserAccessor.ResolveApplicationUserAsync` doc comment expanded** to
  name the upstream writer (`ApplicationUserRoleResolverAdapter` running
  during claims transformation, inside `AuthenticateAsync`) and enumerate
  the legitimate cache-miss cases (operator/machine-track requests,
  tenant-track edge cases like `NoClaimsIdentity` / `NoUserIdentifier`,
  non-HTTP synthesized contexts).

### Updated

- **`Cirreum.Core`** — `4.0.2` → `5.0.1` (transitive major bump). Picks up
  the `AuthenticationContextKeys` static class and
  `IApplicationUserResolver.Scheme` property required for the dispatch
  changes above.

### Migration

Existing single-resolver apps — no code change required. The default
`Scheme => null` on `IApplicationUserResolver` makes the existing resolver
the fallback for any authenticated scheme, which is identical to the prior
single-resolver behavior.

Multi-IdP server hosts can now register multiple resolvers (one per scheme)
via `CirreumAuthorizationBuilder.AddApplicationUserResolver<T>()` — see the
`Cirreum.Runtime.Authorization` release notes for the matching registration-
side change.

