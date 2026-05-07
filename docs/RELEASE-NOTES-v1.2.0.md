# Cirreum.Services.Server 1.2.0 — HTTP→`IInvocationContext` bridge

The seam that anchors [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) lights up for HTTP. This release adds the middleware that publishes `IInvocationContext` for every HTTP request and refactors `UserStateAccessor` to consume it through `IInvocationContextAccessor` instead of `IHttpContextAccessor` directly.

Strictly internal refactor at the framework level. Zero behavior change for existing apps. Zero public API removed.

---

## Why this release exists

`Cirreum.InvocationProvider` 1.0.1 shipped the unified L2 inbound seam — `IInvocationContext`, `IInvocationContextAccessor`, the connection-family abstractions — but until something *populated* the seam, those types were just contracts. This release wires the framework's primary invocation source (HTTP) into the seam.

Once landed, framework-internal code (`UserStateAccessor`, the conductor pipeline, authorizers, audit) reads identity through the unified seam. `IHttpContextAccessor` remains available for app code that needs HTTP-specific concerns (response headers, cookies) but no longer carries identity for the framework. When the SignalR / WebSocket / gRPC source adapters arrive in releases #11–#16, they populate the same seam — and the same `UserStateAccessor` works against them with no further changes.

---

## What's new

### `InvocationContextHttpMiddleware`

Per-request middleware that materializes an `IInvocationContext` for the active `HttpContext` and publishes it through `IInvocationContextAccessor`:

```csharp
public async Task InvokeAsync(HttpContext context) {
    var invocation = new HttpInvocationContext(context);
    this._accessor.Set(invocation);
    try {
        await this._next(context);
    } finally {
        this._accessor.Clear();
    }
}
```

Concrete `HttpInvocationContext`:

| Member | Source | Lifetime |
|---|---|---|
| `User` | snapshot of `HttpContext.User` at middleware entry | immutable for the invocation |
| `Items` | **alias** of `HttpContext.Items` (same dictionary reference) | request lifetime |
| `Services` | `HttpContext.RequestServices` | request lifetime |
| `Aborted` | `HttpContext.RequestAborted` | request lifetime |
| `InvocationSource` | `InvocationSources.Http` | constant |
| `Connection` | `null` | HTTP is stateless |
| `AppName` *(internal)* | snapshot of `RemoteIdentityConstants.AppNameHeader` at middleware entry | immutable for the invocation |

**Items aliasing is the load-bearing decision.** Existing framework code that reads/writes `AuthenticationContextKeys` slots through `HttpContext.Items` continues to work transparently when migrated to `IInvocationContext.Items` — same dictionary, same keys, same values. The role-claims transformer writes; `UserStateAccessor` reads; the per-scheme `IApplicationUserResolver` cache flows through. No behavior changes anywhere.

### `UseInvocationContext()`

`IApplicationBuilder` extension that registers the middleware:

```csharp
app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseInvocationContext();   // ← here: AuthN/AuthZ resolved, ready for endpoint
app.MapEndpoints();
```

**Why late placement.** ASP.NET middleware is FIFO inbound / LIFO outbound. With copy-semantic `User` (snapshot at middleware entry), the snapshot must capture an authenticated principal — so the middleware must run *after* `UseAuthentication` / `UseAuthorization`. By the time the accessor lights up, `User` is fully resolved and `[Authorize]` has already run. Symmetric with the SignalR `InvocationContextHubFilter` that will populate `IInvocationContext` per-Hub-method invocation in release #13: same pattern — populate at the doorstep of work, not at pipeline entry.

### `IInvocationContextAccessor` registration

`AddCoreServices()` now registers `InvocationContextAccessor` as a singleton (matches `IHttpContextAccessor` convention; AsyncLocal-backed, per-flow isolation, no per-scope state on the accessor instance).

---

## What changed in `UserStateAccessor` (renamed from `UserAccessor`)

The implementation type — `internal sealed`, no public API impact — was renamed to `UserStateAccessor` for naming symmetry with the `IUserStateAccessor` interface it implements. Constructor:

```diff
- sealed class UserAccessor(
-     IHttpContextAccessor httpContextAccessor,
-     IWebHostEnvironment webHostEnvironment,
-     IServiceProvider serviceProvider
- ) : IUserStateAccessor { ... }
+ sealed class UserStateAccessor(
+     IInvocationContextAccessor invocationAccessor,
+     IWebHostEnvironment webHostEnvironment
+ ) : IUserStateAccessor { ... }
```

All four enrichment steps switched from `httpContext.X` to `invocation.X`:

| Before | After |
|---|---|
| `_httpContextAccessor.HttpContext.User` | `_invocationAccessor.Current.User` |
| `context.Items[...]` | `invocation.Items[...]` *(aliased — same dict)* |
| `context.RequestServices` | `invocation.Services` |
| `serviceProvider.GetServices<...>()` | `invocation.Services.GetServices<...>()` |

App-name header read is now done **once**, at middleware entry, captured on `HttpInvocationContext.AppName` (internal property). `UserStateAccessor` consumes it via `(invocation as HttpInvocationContext)?.AppName`. No more mid-pipeline `IHttpContextAccessor` re-read; HTTP-specific snapshotting stays at the seam where the rest of the snapshot already happens. Non-HTTP invocation sources don't satisfy the cast — result is `null`, correct since they have no HTTP headers.

---

## Why this is zero behavior change

Two invariants make the refactor a strict no-op for existing apps:

1. **`IInvocationContext.Items` IS `HttpContext.Items`** for HTTP-sourced invocations. Aliased, not copied. Every key read or written by the role-claims transformer, `UserStateAccessor`, the per-scheme dispatch, and any other consumer flows through the same dictionary instance.
2. **`User` snapshot is captured after `UseAuthentication` / `UseAuthorization`** (per the late placement contract). The captured principal is identical to what `HttpContext.User` would return at any point during endpoint execution.

Per-scheme `IApplicationUserResolver` dispatch (shipped 2026-05-01 in 1.1.0) keeps working unchanged — same `AuthenticationContextKeys.AuthenticatedScheme` slot, same lookup logic, just sourced through the new seam.

---

## Coordinated downstream work

This release unblocks #7 in the [Invocation family rollout](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/InvocationContext/03-MIGRATION.md):

- `Cirreum.Runtime.Server` (next, minor) — `Build()` composition will call `app.UseInvocationContext()` after `UseAuthorization()`. Apps using Runtime.Server pick up the seam automatically on package update.

Followed by:

- `Cirreum.Runtime.AuthorizationProvider` (patch) — `Items` reads switch from `IHttpContextAccessor` to `IInvocationContextAccessor` (same rationale; trivial).
- `Cirreum.Runtime.IdentityProvider` (patch, only if `Items` reads exist).
- LapCast / existing apps — app-side patch on package update; no code changes required if using `Cirreum.Runtime.Server`.

Then the Invocation family per-source packages (SignalR, WebSockets, gRPC) light up alongside HTTP through the same seam.

---

## Compatibility

- **Strictly source-compatible** with 1.1.x for downstream consumers — `IUserStateAccessor` contract unchanged, `AddCoreServices()` / `AddGlobalExceptionHandling()` / `AddDefaultHealthChecks()` signatures unchanged.
- **One new public surface:** `UseInvocationContext()` extension on `IApplicationBuilder`.
- **One new package dependency:** `Cirreum.InvocationProvider 1.0.1`.
- **No new public types** beyond the `UseInvocationContext()` extension. `HttpInvocationContext` and `InvocationContextHttpMiddleware` are `internal sealed`.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.2.0`.
- [`Cirreum.InvocationProvider 1.0.1`](https://www.nuget.org/packages/Cirreum.InvocationProvider) — L2 abstractions this release populates.
- [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md) — the foundational design decision.
- [Invocation family migration plan](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/InvocationContext/03-MIGRATION.md) — full rollout sequence.
