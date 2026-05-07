# Cirreum.Services.Server

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Services.Server.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Services.Server/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Services.Server.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Services.Server/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Services.Server?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Services.Server/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Services.Server?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Services.Server/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Infrastructure services for .NET server applications**

## Overview

**Cirreum.Services.Server** provides essential infrastructure services for .NET server applications (Web API and Web App). This library offers a comprehensive foundation with enterprise-grade patterns for exception handling, caching, security, health checks, and file system operations.

## Features

- **Invocation Context Bridge**: HTTP→`IInvocationContext` middleware that publishes a unified, transport-agnostic per-invocation seam consumed by framework code (CQRS handlers, authorization, audit, repositories) — see [ADR-0002](https://github.com/cirreum/Cirreum.DevOps/blob/main/docs/adr/0002-unified-invocation-context.md)
- **Global Exception Handling**: RFC 7807 compliant Problem Details with environment-aware responses
- **Hybrid Caching**: Modern caching infrastructure with tag-based invalidation and smart expiration policies  
- **Security Services**: Claims-based user context management and authentication integration
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
app.UseAuthentication();
app.UseAuthorization();
app.UseInvocationContext();   // ← here: AuthN/AuthZ resolved, ready for endpoint
app.MapEndpoints();
```

> Apps using `Cirreum.Runtime.Server`'s `Build()` composition pick up `UseInvocationContext()` automatically — no manual wiring required.

## Service Registration

The library provides extension methods for clean service registration:

- `AddCoreServices()` - Registers `IInvocationContextAccessor` (singleton, AsyncLocal-backed), file system, datetime, security, and caching services
- `AddGlobalExceptionHandling()` - Configures RFC 7807 exception handling pipeline
- `AddDefaultHealthChecks()` - Sets up health check infrastructure with startup monitoring

## Pipeline Extensions

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

Given its foundational role, major version bumps are rare and carefully considered.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*