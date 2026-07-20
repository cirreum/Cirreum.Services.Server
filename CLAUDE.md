# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

```bash
# Restore dependencies
dotnet restore Cirreum.Services.Server.slnx

# Build the solution
dotnet build Cirreum.Services.Server.slnx --configuration Release

# Run tests (dedicated test solution — tests are NOT in the main slnx)
dotnet test tests/Cirreum.Services.Server.Tests.slnx

# Pack for NuGet distribution
dotnet pack Cirreum.Services.Server.slnx --configuration Release
```

## Architecture Overview

This is the **server-side services library** for Cirreum ASP.NET applications
(Infrastructure layer). It owns the framework's server-side invocation surface —
HTTP, WebSocket, and SignalR invocation contexts behind one abstraction — plus the
per-server connection registry, the auth-event connection terminator, server user-state
assembly, global exception handling, and supporting services (clock, file system,
health probes).

### Core Components Structure

- **Extensions/Hosting** (`HostingExtensions.cs`) — the registration surface
  (`AddCoreServices`, `AddGlobalExceptionHandling`, …)
- **Invocation** — `InvocationContextAccessor` plus `Invocation/Connections`: the
  per-server `InvocationConnectionRegistry`, its `ConnectionRegistryLifecycle`, and
  the `ConnectionTerminationHandler` (an auth-event consumer — revocation / forced
  sign-out aborts a subject's live long-lived connections)
- **Http / WebSockets / SignalR** — per-transport invocation contexts and adapters
  (`HttpInvocationContext` + middleware, `WebSocketHandler`/`WebSocketOrchestrator`
  + connection, `InvocationContextHubFilter` + `SignalRConnection` +
  `CirreumUserIdProvider`), each establishing the same per-invocation DI scope and
  `Items` semantics so ambient consumers work identically across transports
- **Security** — `UserStateAccessor` (the `IUserStateAccessor` implementation),
  `ServerUserState`/`ServerUserBase`
- **Diagnostics** — RFC 7807 Problem Details via `GlobalUnhandledExceptionHandler`,
  exception-model JSON setup
- **Http/Filters** — `ResultToHttpEndpointFilter` (Cirreum.Result → HTTP responses)
- **Health** — startup/started-and-alive probes
- **Clock / FileSystem** — timezone-aware `IDateTimeClock`, local file system + CSV
  services with Polly-based retry

### User-State Assembly and the Authentication Boundary

`UserStateAccessor` assembles the per-invocation `ServerUserState` in ordered steps —
principal snapshot, application-user resolution, then **authentication-boundary
stamping**: it resolves `IAuthenticationBoundaryResolver` (from `Cirreum.Kernel`,
namespace `Cirreum.Security`) out of the invocation's scope and stamps the verdict;
a missing resolver stamps `None`. `AddCoreServices()` `TryAdd`-registers the Kernel
default resolver (authenticated → `Global`) alongside the accessor — the consumer of
the seam guarantees one exists. A scheme-aware resolver (registered by
`Cirreum.Runtime.Authentication` where `PrimaryScheme` is read) or an app-registered
custom resolver wins when registered first.

### Dependencies

- **Cirreum.Domain** — the framework spine; brings `Cirreum.Contracts`,
  `Cirreum.Kernel` (user-state + boundary contracts, auth events,
  `AuthenticationContextKeys`), `Cirreum.Exceptions`, and `Cirreum.Result`
  transitively. This package deliberately references **no authentication-track
  packages**.
- **FluentValidation** — validation integration
- **Polly** (+ `Polly.Contrib.WaitAndRetry`) — resilience and retry policies
- **Microsoft.AspNetCore.App** — framework reference

### Testing

`tests/Cirreum.Services.Server.Tests.slnx` — xUnit + FluentAssertions + NSubstitute.
Covers the connection registry and its lifecycle, the auth-event connection
terminator, the SignalR user-id provider, and service registration.

### Build Configuration

- Multi-environment CI detection (Azure DevOps, GitHub Actions) with tag-derived
  versioning in CI; local Release builds use the `1.0.100-rc` convention
- Shared MSBuild properties split across `build/*.props`
- `InternalsVisibleTo` for the test assembly

### Development Notes

- Target framework: .NET 10.0 with latest C# language version
- Implicit usings and nullable reference types enabled; tabs + K&R per `.editorconfig`
- Package generation happens through CI/CD (`publish.yml` builds the main slnx only —
  tests never affect packaging)
