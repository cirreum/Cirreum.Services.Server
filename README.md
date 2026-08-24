# Cirreum.Services.Server

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Services.Server.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Services.Server/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Services.Server.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Services.Server/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Services.Server?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Services.Server/releases)
[![License](https://img.shields.io/badge/license-MIT-F2F2F2?style=flat-square&labelColor=1F1F1F)](https://github.com/cirreum/Cirreum.Services.Server/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Infrastructure services for .NET server applications**

## Overview

**Cirreum.Services.Server** provides essential infrastructure services for .NET server applications (Web API and Web App). This library offers a comprehensive foundation with enterprise-grade patterns for invocation contexts, user-state assembly, exception handling, health checks, and file system operations.

## Features

- **Invocation Contexts**: HTTP, WebSocket, and SignalR invocations behind one transport-agnostic `IInvocationContext` seam consumed by framework code (CQRS handlers, authorization, audit, repositories); long-lived transports seed each invocation with the connection's authentication slots (`AuthenticatedScheme`, `OriginScheme`, `ApplicationUserCache`) so subject facts read uniformly across sources
- **Connection Registry & Termination**: a per-server registry of active long-lived connections (SignalR, WebSocket) plus the framework-shipped terminator that reacts to `CredentialRevoked` / `UserAccountDisabled` / `SessionTerminationRequested` auth events by aborting the subject's live connections — revocation reaches open sockets, not just future requests. Honors Two-Phase Auth promotion (promoted connections are attributed to their promoted identity)
- **Global Exception Handling**: RFC 7807 compliant Problem Details with environment-aware responses
- **User-State Assembly**: per-invocation `IUserState` built from the snapshotted principal — subject kind resolved from the effective scheme's declaration (`ISchemeClaimAuthorityMap`, optional), application-user and boundary resolution dispatched on the effective scheme, and a fill-only app-name fallback for machine callers
- **Health Checks**: Application readiness probes and startup health monitoring
- **File System Services**: Resilient local file operations with CSV processing capabilities
- **DateTime Services**: Timezone-aware clock services with TimeProvider integration
- **Dependency Injection**: Clean service registration patterns following .NET conventions

## Quick Start

Install the package:

```bash
dotnet add package Cirreum.Services.Server
```

Register services in your application:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Add core infrastructure services
builder.Services.AddCoreServices();

// Add global exception handling
builder.Services.AddGlobalExceptionHandling();

// Add health checks with startup probe
builder.Services.AddDefaultHealthChecks();
```

Wire the invocation-context bridge into your HTTP pipeline. **Placement matters** — register *after* authentication and authorization so the snapshotted `IInvocationContext.User` reflects the fully-resolved authenticated principal:

```csharp
using Microsoft.AspNetCore.Builder;

app.UseExceptionHandler();
app.UseRouting();
app.UseConnectionCredential();   // ← routing resolved, before any scheme reads the header
app.UseAuthentication();
app.UseAuthorization();
app.UseInvocationContext();   // ← here: AuthN/AuthZ resolved, ready for endpoint
app.MapApiEndpoints();
```

> Apps using `Cirreum.Runtime.Server`'s `Build()` composition pick up both `UseConnectionCredential()` and `UseInvocationContext()` automatically — no manual wiring required.

## Service Registration

The library provides extension methods for clean service registration:

- `AddCoreServices()` - Registers `IInvocationContextAccessor` (singleton, AsyncLocal-backed), user-state assembly (with the Kernel default authentication-boundary resolver), file system, and datetime services
- `AddGlobalExceptionHandling()` - Configures RFC 7807 exception handling pipeline
- `AddDefaultHealthChecks()` - Sets up health check infrastructure with startup monitoring

## Pipeline Extensions

- `UseConnectionCredential()` - Promotes a query-carried bearer credential (`?access_token=…`) into the `Authorization` header on SignalR hubs and other connection endpoints, so every authentication scheme reads it where it always has. A browser cannot set headers on a WebSocket upgrade, so this is the only credential such a client can present; without it SignalR silently falls back to Server-Sent Events or long polling. Scoped to connection endpoints, and never overrides an `Authorization` header that is already present. Register between `UseRouting()` and `UseAuthentication()`.
- `UseInvocationContext()` - Publishes an `IInvocationContext` for every HTTP request through `IInvocationContextAccessor`. Snapshots `User` (immutable for the invocation), aliases `HttpContext.Items` (same dictionary reference — existing `AuthenticationContextKeys` slots flow through transparently), and exposes `RequestServices` / `RequestAborted` through the unified seam. Register late — after `UseAuthentication()` / `UseAuthorization()`, before endpoint execution.

## Architecture

Built on the `IServerDomainApplicationBuilder` pattern extending `IDomainApplicationBuilder`, providing:

- Configuration management through `IConfigurationManager`
- Host environment information via `IHostEnvironment`  
- Deferred logging capabilities for startup diagnostics

## Contribution Guidelines

1. **Be conservative with new abstractions**  
   The API surface must remain stable and meaningful.

2. **Limit dependency expansion**  
   Only add foundational, version-stable dependencies.

3. **Favor additive, non-breaking changes**  
   Breaking changes ripple through the entire ecosystem.

4. **Include thorough unit tests**  
   All primitives and patterns should be independently testable.

5. **Document architectural decisions**  
   Context and reasoning should be clear for future maintainers.

6. **Follow .NET conventions**  
   Use established patterns from Microsoft.Extensions.* libraries.

## Versioning

Cirreum.Services.Server follows [Semantic Versioning](https://semver.org/):

- **Major** - Breaking API changes
- **Minor** - New features, backward compatible
- **Patch** - Bug fixes, backward compatible

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*