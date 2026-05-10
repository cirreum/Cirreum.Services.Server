# Cirreum.Services.Server 1.2.4 — `UserStateAccessor` future-proofs the lazy-resolve cache for long-lived connections

Future-proofs the framework for the AI/LLM act-on-behalf-of seam (the "Piece 2" follow-on to the in-flight invocation-context auth-slot work) by making `UserStateAccessor.ResolveApplicationUserAsync` write the resolved `IApplicationUser` to BOTH per-invocation `Items` AND per-connection `Connection.Items` on the lazy-resolve path. Today the propagation line is dead-code for all current `IApplicationUserResolver` registrations; the day a null-scheme resolver registers (which Piece 2 will), it prevents what would otherwise be an extremely subtle "why is the IdP being hammered every Hub method / WebSocket message" defect six months from now.

Parallel to the auth-slot copy + per-invocation seed fix shipping in `Cirreum.Invocation.SignalR 1.2.1` and `Cirreum.Invocation.WebSockets 1.2.1`.

---

## What changed

`UserStateAccessor.ResolveApplicationUserAsync` lazy-resolve path:

```diff
  if (resolver is not null) {
      var appUser = await resolver.ResolveAsync(user.Id);
      if (appUser is not null) {
          user.SetResolvedApplicationUser(appUser);
          invocation.Items[AuthenticationContextKeys.ApplicationUserCache] = appUser;
+         // Future-proof for long-lived sources: when a null-scheme resolver eventually
+         // fires for header-auth or M2M-on-behalf-of-human (the AI/LLM Piece 2 seam),
+         // propagate the resolved user to the connection-lifetime bag so subsequent
+         // invocations on the same connection seed correctly via the L3 adapter's
+         // per-invocation Items copy.
+         if (invocation.Connection is { } connection) {
+             connection.Items[AuthenticationContextKeys.ApplicationUserCache] = appUser;
+         }
          return;
      }
  }
```

---

## Why this release exists

Today (immediately after `Cirreum.Invocation.SignalR 1.2.1` and `Cirreum.Invocation.WebSockets 1.2.1` ship):

- **Audience-auth on long-lived**: `ApplicationUserCache` is populated at upgrade by the audience-auth claims-transformer's role-resolver path (`ApplicationUserRoleResolverAdapter`). The L3 adapter copies it onto `Connection.Items` at upgrade and seeds it onto every per-invocation `Items` bag. `UserStateAccessor` reads `invocation.Items` and HITS. The lazy-resolve path doesn't fire. The new propagation line is dead-code in this scenario.
- **Header-auth on long-lived** (API key, signed request): no `IApplicationUserResolver` registers a matching scheme (these auth shapes hand-craft `ClaimsPrincipal` directly with roles from app-settings or a DB lookup at auth time). `UserStateAccessor.ResolveApplicationUserAsync` short-circuits at the no-matching-resolver early return (line 151 of `UserStateAccessor.cs`) before reaching the lazy-resolve write. The new propagation line is dead-code in this scenario too.
- **HTTP**: `Connection` is null, so the `if (invocation.Connection is { } connection)` branch trivially skips. Behavior identical to before — write only to `invocation.Items` (which aliases `HttpContext.Items` for HTTP, persisting for the request).

So today the line never executes. **It's pure insurance.**

The day a future Piece 2 release (AI act-on-behalf-of) registers a null-scheme `IApplicationUserResolver` to map M2M / API-key callers onto an "effective human user" the LLM is acting for, the lazy-resolve path goes live for long-lived connections. Without this propagation line, every per-Hub-method / per-WebSocket-message invocation would re-invoke the resolver because the per-invocation cache write doesn't survive the invocation. With it, the resolved user lands on `Connection.Items` and the next invocation's per-invocation seed picks it up cleanly via the L3 adapter's seed step.

The framework decision: pay the one-line cost now, while the change is small and the rationale is fresh, rather than discover IdP hammering when Piece 2 ships and have to dig through three repos to figure out why.

---

## Why double-write rather than read-side fallback

Two patterns considered:

**Read-side fallback**: `UserStateAccessor` reads from `invocation.Items` first, falls back to `invocation.Connection?.Items`. Or uses a single bag-pick: `var bag = invocation.Connection?.Items ?? invocation.Items`.

**Write-side double-write** (chosen): `UserStateAccessor` reads `invocation.Items` only. On lazy resolve, writes to both bags.

The double-write keeps **all readers** (UserStateAccessor itself, plus any other framework or app consumer of the cache) **ignorant of the connection bag**. Readers always read `invocation.Items` and get whatever the L3 adapter seeded for them. Only the writer needs to know about Connection.Items, and only on the lazy-resolve path.

The read-side fallback would force every consumer of the cache to use the same bag-pick pattern, creating a subtle coupling that breaks when a future consumer naively reads `invocation.Items` directly (the obvious target). The write-side approach contains the awareness to one place.

---

## Coordinated work

Ships in lockstep with:

- **`Cirreum.Invocation.SignalR 1.2.1`** — `InvocationContextHubFilter.OnConnectedAsync` copies upgrade-time auth slots onto `Connection.Items`; `SignalRInvocationContext` seeds per-invocation `Items` from `Connection.Items` at construction.
- **`Cirreum.Invocation.WebSockets 1.2.1`** — same fix for the WebSocket adapter (`WebSocketOrchestrator` + `WebSocketInvocationContext`).

---

## Compatibility

- **Source- and binary-compatible.** No public API change.
- **Behavior-compatible.** The new write line never executes for any current `IApplicationUserResolver` registration — it sits dormant until Piece 2 introduces a null-scheme resolver.

---

## See also

- `CHANGELOG.md` — condensed change list.
- [`Cirreum.Invocation.SignalR 1.2.1`](https://www.nuget.org/packages/Cirreum.Invocation.SignalR) — adapter-side upgrade-time copy + per-invocation seed.
- [`Cirreum.Invocation.WebSockets 1.2.1`](https://www.nuget.org/packages/Cirreum.Invocation.WebSockets) — adapter-side upgrade-time copy + per-invocation seed.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — transport-adapter invariants #2 (upgrade-time slot copy) and #6 (per-connection / per-invocation bag separation).
