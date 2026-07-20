# Cirreum.Services.Server 1.4.1 — Boundary default registration moved to the spine

Corrects 1.4.0, released earlier the same day: the default
`IAuthenticationBoundaryResolver` registration is removed from this package.

## Why this release exists

1.4.0 registered the default resolver inside `AddCoreServices()` — which runs at
builder *construction*, before the application's composition. A `TryAdd` at that
point wins against everything registered later, so the Authentication track's
scheme-aware resolver (primary scheme → `Global`, other authenticated schemes →
`Tenant`, registered during `AddAuthentication()`) could never take effect: every
caller would have fallen back to the blanket default. The last-chance default
belongs where it always ran — in `Cirreum.Runtime.Server` at `Build()` time, after
composition — and that is where it now lives. `UserStateAccessor` tolerates absence
by stamping `None`.

## Compatibility

- No API change. Hosts composed via `Cirreum.Runtime.Server` get the identical
  default (authenticated → `Global`) from the spine's `Build()`-time registration,
  and scheme-aware / app-registered resolvers win as intended.
- Hosts using this package **without** `Cirreum.Runtime.Server` and without any
  resolver registration now stamp `None` (unresolved) instead of the blanket
  `Global` — register the Kernel default or a custom resolver explicitly if
  boundary classification is consumed in such a host.

## See also

- `RELEASE-NOTES-v1.4.0.md` — the seam relocation this release corrects
- `Cirreum.Runtime.Server` — the `Build()`-time last-chance registration
