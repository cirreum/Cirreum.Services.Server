# Cirreum.Services.Server 1.7.0 — The host invocation factory

## Why this release exists

`Cirreum.Contracts` 6.2.0 declared `IHostInvocationFactory`: the seam through which work the
host starts on its own behalf gets a real `IInvocationContext` instead of running as the
anonymous caller. This release implements it, on the same package that owns the HTTP,
SignalR and WebSocket invocation contexts, so every way work enters the framework now
produces the same ambient context.

## What's new

### `HostInvocationFactory`, registered by `AddCoreServices`

```csharp
await using var invocation = factory.Begin();
// invocation.Context is current: scoped services, Aborted, a host principal
```

`Begin`:

1. **Refuses to nest.** If an invocation is already current on the calling flow it throws,
   naming `ExecutionContext.SuppressFlow()` and `StartDetached` as the two ways out. A
   request context flowing into background work is the hazard; failing here is the check.
2. **Creates a root-derived async service scope** — `IInvocationContext.Services`.
3. **Links cancellation** to `IHostApplicationLifetime.ApplicationStopping` and the caller's
   token — `IInvocationContext.Aborted`.
4. **Stamps a fresh `Items` bag** with `AuthenticatedScheme = Cirreum.Host`, plus
   `OriginScheme` when a caller-supplied subject carries one.
5. **Sets the accessor** and returns the handle. Disposal clears the accessor, releases the
   token source and disposes the scope. Disposal is deliberately synchronous on the
   `AsyncLocal`: an `async` dispose would clear a copy of the caller's context and the next
   `Begin` on that flow would throw as nested.

### The host principal

With no `User` supplied, the invocation's principal names the host application: `sub` and
`name` are `IHostEnvironment.ApplicationName`, the authentication type is `Cirreum.Host`, and
the identity is minted per invocation, never shared — `UserStateAccessor` rewrites the
app-name claim on whatever identity it is handed, so a shared one would be mutated across
threads.

`AddCoreServices` files a `SchemeClaimAuthorityRegistration` declaring `Cirreum.Host` as
`SubjectKind.Machine`. On a host that composes authentication, user state for host work
resolves as a machine subject with the application's name, no application user, and the
boundary the registered resolver assigns the scheme. Audit columns read `LapCast.Api`, not
`Guest`.

### Roles

```csharp
await using var invocation = factory.Begin(new() { Roles = ["Importer"] });
```

Roles are minted as role claims on the host identity. `null` takes the composition-time
default, `HostInvocationFactoryOptions.DefaultRoles`, bound from
`Cirreum:Invocation:Host:DefaultRoles` and empty unless configured. An empty list mints none.
Host work that declares no roles passes no `HasRole` gate — the safe default, and a denial
that names the host rather than `Guest`.

### Continuations

```csharp
var user = invocation.User;                 // snapshot before suppressing flow
var origin = invocation.EffectiveScheme;
await factory.StartDetached(
    ctx => FinishAsync(ctx),
    new() { User = user, OriginScheme = origin, Roles = ["Importer"] });
```

A supplied principal is cloned, never modified. Supplied roles go on a separate
`Cirreum.Host` identity added beside the caller's, so `IsInRole` sees both sets while id,
name and issuer still resolve from the caller's primary identity. `OriginScheme` makes
subject kind, boundary and application user resolve for the subject: a tenant user's
continuation stays a tenant subject.

### `UserStateAccessor`

Reads the caller's app name through an internal seam shared by the HTTP and host contexts
rather than a cast to the HTTP type. No behaviour change for HTTP invocations.

## Compatibility

Additive. `AddCoreServices` registers one more singleton, one options binding and one scheme
declaration. An application that already declares a scheme named `Cirreum.Host` differently
fails at composition, which is the existing rule for conflicting declarations.

## See also

- `docs/CHANGELOG.md` — `[1.7.0]`
- `Cirreum.Contracts` 6.2.0 release notes — the contract and `StartDetached`
