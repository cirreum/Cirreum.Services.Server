# Cirreum.Services.Server 1.5.0 — the subject resolves through the scheme that established it

## Why this release exists

A DB-owns subject carries a deliberately thin token — the application is the authority, so the
IdP sends little. The server's user-state assembly used to read that thinness as evidence of
something else: a blank name meant "machine caller," and the unauthenticated app-name header
became the subject's Name claim. The visible symptom was an authorization audit line naming
the calling application as the user.

The fix is the attribute-authority model: schemes *declare* what kind of subject they
authenticate, and the framework reads the declaration instead of inferring from token
contents. This release is the consumer side of that model — the readers. They resolve
everything through contracts that already shipped (`Cirreum.Kernel`'s
`ISchemeClaimAuthorityMap`, `Cirreum.Contracts`' `EffectiveScheme`), and every new path
degrades to today's behavior until the authentication runtime registers the declaration map.

## What's new

**Subject-kind resolution at step 0.** `UserStateAccessor` resolves the invocation's
effective scheme — `OriginScheme ?? AuthenticatedScheme` — through `ISchemeClaimAuthorityMap`
and stamps `IUserState.SubjectKind` before any enrichment runs. The map is optional: no
registered map, no classification, kind stays `Unknown`.

**Effective-scheme dispatch.** Application-user resolver dispatch and
authentication-boundary resolution key on the effective scheme rather than the authenticated
scheme alone. A subject that reaches the server through a session-ticket continuation or a
Two-Phase Auth promotion resolves through the scheme that *established* it — a DB-owns user
reconnecting over a ticketed WebSocket hits their IdP's resolver, not the ticket's.

**`OriginScheme` travels the long-lived transports.** The WebSocket orchestrator and the
SignalR hub filter copy the handshake stamp onto `Connection.Items` beside
`AuthenticatedScheme` and `ApplicationUserCache`, and both per-invocation contexts seed it
into invocation items — so the scheme triple reads uniformly whether the invocation arrived
over HTTP, WebSocket, or SignalR.

**The app-name fallback is fill-only and machine-gated.** Naming a machine caller from the
`X-Cirreum-App-Name` header remains supported, but the header value only ever *fills a gap* —
the removal of existing name claims is gone, so an unauthenticated header can never displace
credential-derived claims. Once a declaration map is registered, the fallback additionally
applies only to `SubjectKind.Machine` subjects: a person with a thin token is never named
after the calling application. Without a map, the legacy blank-name gate stands unchanged.

## How it degrades

Every reader in this release is dormant-by-default. No declaration map registered means:
subject kind stays `Unknown`, the machine gate falls back to the legacy blank-name heuristic,
and effective-scheme dispatch equals authenticated-scheme dispatch wherever no origin is
stamped. The one behavioral change that ships active is the fill-only fix — and that is a
correctness repair, not a feature toggle.

## Compatibility

- No public surface changes; all changes are internal to user-state assembly and the
  transport adapters.
- Behavior changes only where a credential minted name claims the old code would have
  removed (fill-only), or where an origin stamp is present (effective-scheme dispatch) —
  today that means session-ticket flows, which previously mis-dispatched to the ticket
  scheme's resolver.

## See also

- `Cirreum.Contracts 4.4.0` — `OriginScheme` / `EffectiveScheme`, the read surface this
  release consumes.
- `Cirreum.Kernel 2.1.1` — `ISchemeClaimAuthorityMap` and `SubjectKind`, the declaration
  contracts.
- `Cirreum.Authentication.SessionTicket 1.1.0` — stamps the origin these readers resolve.
