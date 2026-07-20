# Cirreum.Services.Server 1.4.0 — Boundary resolution wired, auth-track dependency dropped

> **Superseded on the registration point by 1.4.1** (same day): the default-resolver
> registration described below ran too early (builder construction) and pre-empted
> scheme-aware resolvers; it moved to `Cirreum.Runtime.Server` at `Build()` time.
> See `RELEASE-NOTES-v1.4.1.md`.

`UserStateAccessor` stamps the caller's `AuthenticationBoundary` on every invocation —
but since the Foundation Reset, no package registered an
`IAuthenticationBoundaryResolver`, so the accessor's null-fallback stamped
`AuthenticationBoundary.None` on every user state and grant providers gating on
`Global`/`Tenant` could never pass. This release makes the consumer of the seam
guarantee it exists, and in doing so removes this package's last dependency on the
Authentication track's contracts.

---

## Why this release exists

Boundary resolution is spine infrastructure: this package's user-state pipeline
resolves it per invocation whether or not any authentication scheme is composed. The
seam formerly lived in `Cirreum.AuthenticationProvider` — placed there, per its own
docs, *because* the spine needed it — which forced this package to reference the
Authentication track for exactly one interface, and left the seam's registration
ownerless (a "spine registration" extension existed, but nothing in the spine called
it). The seam now lives in `Cirreum.Kernel` (`Cirreum.Security`), beside the
`AuthenticationBoundary` enum, `IUserState`, and `UserStateBase` it operates on.

## What's new

**The default resolver is registered where it's consumed.** `AddCoreServices()`
`TryAdd`-registers the Kernel default (`DefaultAuthenticationBoundaryResolver`:
authenticated → `Global`, unauthenticated → `None`) alongside the
`UserStateAccessor` registration. Registration intent is structural — the package
that resolves the seam registers the fallback it requires, so it can never be
orphaned again. `TryAdd` semantics throughout: a scheme-aware resolver (registered
by `Cirreum.Runtime.Authentication` 1.2.0 where `PrimaryScheme` is read) or an
app-registered custom resolver wins when registered first.

**The `Cirreum.AuthenticationProvider` reference is gone.** Boundary types are
consumed from `Cirreum.Kernel`, which this package already carried transitively
through `Cirreum.Domain`. The services spine no longer depends on any
authentication-track package.

## Compatibility

- **Boundary values change on upgrade.** Hosts move from the all-`None` regression
  to real classification (`Global` for every authenticated caller under the default;
  `Global`/`Tenant` under the umbrella's primary-scheme resolver). Code that
  accidentally depended on `None` for authenticated callers sees corrected values.
- **Transitive dependency change.** `Cirreum.AuthenticationProvider` no longer flows
  through this package. Applications consuming its types transitively via
  `Cirreum.Services.Server` alone must reference it directly — apps composing
  `Cirreum.Runtime.Authentication` are unaffected (it carries the track).
- Requires `Cirreum.Domain` 1.2.7+ (floors `Cirreum.Kernel` 1.2.0, the seam's home).

## See also

- `Cirreum.Kernel` 1.2.0 — `IAuthenticationBoundaryResolver` and the default resolver
- `Cirreum.Runtime.Authentication` 1.2.0 — the scheme-aware primary-scheme resolver
- `Cirreum.Services.Serverless` — the same seam wired for the Functions host
