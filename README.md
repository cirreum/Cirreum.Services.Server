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

## Service Registration

The library provides extension methods for clean service registration:

- `AddCoreServices()` - Registers file system, datetime, security, and caching services
- `AddGlobalExceptionHandling()` - Configures RFC 7807 exception handling pipeline
- `AddDefaultHealthChecks()` - Sets up health check infrastructure with startup monitoring

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