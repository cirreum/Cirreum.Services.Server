# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.2] - 2026-05-09

### Updated

- Updated NuGet packages.

## [1.2.1] - 2026-05-08

### Updated

- Updated NuGet packages.

## [1.2.0] - 2026-05-07

### Added

- **`InvocationContextHttpMiddleware`** — per-request middleware that materializes an `IInvocationContext` for the active `HttpContext` and publishes it through `IInvocationContextAccessor`. Snapshots `User` (immutable for the invocation), aliases `HttpContext.Items` (same dictionary reference — no copy), and exposes `RequestServices` / `RequestAborted` through the seam.
- **`UseInvocationContext()`** extension on `IApplicationBuilder` — registers the bridge middleware. Intended placement: after `UseAuthentication` / `UseAuthorization`, before endpoint execution. `Cirreum.Runtime.Server` will pick this up in its `Build()` composition (release #7 in the [Invocation family rollout](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/InvocationContext/03-MIGRATION.md)).
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

