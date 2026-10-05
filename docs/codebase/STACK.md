# Technology Stack

## Core Runtime
- **.NET 8.0** (LTS) — Target framework for all projects
- **WPF** (`UseWPF=true`) — Desktop UI framework, WinExe output type

## Project Structure (Clean Architecture)
| Project | Type | Target Framework | Purpose |
|---------|------|------------------|---------|
| `NEO-e.Domain` | Class Library | net8.0 | Domain entities, value objects, exceptions |
| `NEO-e.Application` | Class Library | net8.0 | Use cases, application contracts (interfaces) |
| `NEO-e.Infrastructure` | Class Library | net8.0 | External adapters (HTTP, persistence, certificates, file system, logging) |
| `NEO-e.Contracts` | Class Library | net8.0 | DTOs and external API contracts (separated from domain) |
| `NEO-e.App` | WPF Application | net8.0-windows | Main entry point, ViewModels, Views |

## Key Dependencies (Production)

### NEO-e.Application
- `System.Security.Cryptography.X509Certificates` 4.3.2

### NEO-e.Infrastructure
- `Microsoft.Data.Sqlite` 8.0.0 — Embedded SQLite database
- `Microsoft.Extensions.Configuration.Binder` 8.0.0
- `Microsoft.Extensions.Configuration.Json` 8.0.0
- `Microsoft.Extensions.DependencyInjection` 8.0.0
- `Microsoft.Extensions.Http` 8.0.0
- `Microsoft.Extensions.Options` 8.0.0
- `Serilog` 4.0.0 — Structured logging
- `Serilog.Extensions.Hosting` 8.0.0
- `Serilog.Settings.Configuration` 8.0.0
- `Serilog.Sinks.File` 5.0.0 — File sink with rolling

### NEO-e.App
- `Microsoft.Extensions.Hosting` 8.0.0
- `Serilog.Extensions.Hosting` 8.0.0

## Development Tooling
- **MSBuild** / `dotnet` CLI
- **xUnit** (test framework, referenced in test project)
- **Serilog** for logging (configured via appsettings.json)

## Configuration
- `appsettings.json` — Main configuration (certificates, storage, ADN API, environment, logging)
- Strongly-typed `IOptions<>` pattern with validation on startup
- Environment separation: `Restrita` (homologation) vs `Producao` with explicit confirmation required

## Language Features
- C# 12 (implicit usings, nullable reference types, record structs, primary constructors)
- `readonly record struct` for value objects (CNPJ, NSU, ChaveAcesso, ValorMonetario)
- `sealed` classes for entities and use cases
- Pattern matching, switch expressions, init-only properties

## Evidence
- `src/NEO-e.Domain/NEO-e.Domain.csproj`
- `src/NEO-e.Application/NEO-e.Application.csproj`
- `src/NEO-e.Infrastructure/NEO-e.Infrastructure.csproj`
- `src/NEO-e.Contracts/NEO-e.Contracts.csproj`
- `src/NEO-e.App/NEO-e.App.csproj`
- `src/NEO-e.App/appsettings.json`
- `src/NEO-e.Infrastructure/Configuration/AppSettings.cs`