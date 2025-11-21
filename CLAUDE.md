# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

```bash
# Restore dependencies
dotnet restore Cirreum.Services.Server.slnx

# Build the solution
dotnet build Cirreum.Services.Server.slnx --configuration Release

# Run tests (when test projects exist)
dotnet test Cirreum.Services.Server.slnx

# Pack for NuGet distribution
dotnet pack Cirreum.Services.Server.slnx --configuration Release

# Build for local development
dotnet build Cirreum.Services.Server.slnx --configuration Debug
```

## Architecture Overview

This is a **service library** for .NET 10.0 server applications, designed to be consumed as a NuGet package. It provides infrastructure services following clean architecture principles.

### Key Architectural Patterns

- **Dependency Injection**: Extension method-based service registration pattern using `IServiceCollection`
- **Domain-Driven Design**: Custom `IServerDomainApplicationBuilder` extending base domain application builder
- **Global Exception Handling**: RFC 7807 compliant Problem Details with environment-aware behavior
- **Hybrid Caching**: Modern caching with tag-based invalidation and smart expiration policies
- **Resilience Patterns**: Polly-based retry policies for file operations and external dependencies

### Core Components Structure

- **Extensions/Hosting**: Service registration extensions (`HostingExtensions.cs`)
- **Security**: User context management with claims-based authentication
- **Health**: Application readiness probes and health check infrastructure  
- **FileSystem**: Local file operations with retry policies
- **Clock**: Timezone-aware DateTime services with `TimeProvider` integration
- **Conductor/Caching**: Query result caching with failure-aware expiration
- **Diagnostics**: Exception mapping and JSON serialization setup

### Build Configuration

The project uses a sophisticated build system with:
- **Multi-Environment Detection**: Automatically detects CI/CD environments (Azure DevOps, GitHub Actions)
- **Conditional Compilation**: Different versioning for local vs CI builds
- **Shared Properties**: Build configuration split across multiple `.props` files in the `build/` directory
- **InternalsVisibleTo**: Test assemblies have access to internal members for local development

### Testing Strategy

Test projects are configured through `Directory.Build.props`:
- `Cirreum.Conductor.Tests`
- `Cirreum.ResultMonad.Tests`  
- `Cirreum.Conductor.Benchmarks`

### Dependencies

Key external dependencies to be aware of:
- **Polly**: Resilience and retry policies
- **Cirreum.Core**: Base framework library
- **Microsoft.Extensions.Caching.Hybrid**: Modern hybrid caching
- **ASP.NET Core Framework**: Web application foundation

### Development Notes

- Target framework: .NET 10.0 with latest C# language version
- Implicit usings and nullable reference types enabled
- Package generation happens through CI/CD, not on local builds
- Local development uses version `1.0.100-rc` while CI uses tag-based versioning