# Conventions

## C# Language & Style
- **Target**: .NET 8, C# 12
- **Nullable**: `<Nullable>enable</Nullable>` in all projects
- **Implicit Usings**: `<ImplicitUsings>enable</ImplicitUsings>`
- **Warnings as Errors**: Enforced in CI (baseline clean)

## Naming Conventions
| Element | Convention | Example |
|---------|------------|---------|
| Namespaces | PascalCase, folder-aligned | `NEO_e.Domain.Entities` |
| Classes/Records | PascalCase, `sealed` preferred | `Empresa`, `DocumentoFiscal` |
| Value Objects | `readonly record struct`, PascalCase | `Cnpj`, `Nsu`, `ChaveAcesso` |
| Interfaces | `I` prefix, PascalCase | `IAdnClient`, `INsuRepository` |
| Methods | PascalCase | `ExecuteAsync`, `GetAsync`, `SaveAsync` |
| Parameters | camelCase | `ct`, `empresa`, `certificate` |
| Private Fields | `_camelCase` | `_settings`, `_logger` |
| Constants | PascalCase | `SectionName`, `FullLength` |
| Enums | PascalCase, singular | `EmpresaStatus`, `SyncErrorCode` |
| Test Methods | `Fact`/`Theory` with descriptive names | `Nsu_NaoPodeRegredir` |

## File Organization
- One type per file (exceptions: small related records in same file)
- File name matches type name (`Empresa.cs`, `SyncUseCases.cs`)
- Folders mirror namespaces
- No `#region` directives

## Async Patterns
- **All** I/O operations are `async Task`/`Task<T>` with `CancellationToken`
- `CancellationToken` is **last** parameter, named `ct`
- `ct.ThrowIfCancellationRequested()` at loop boundaries and before external calls
- No `async void` — only `Task` returning methods
- `ConfigureAwait(false)` not used (WPF UI thread captured via `SynchronizationContext`)

## Error Handling
- **Domain Exceptions**: Typed exceptions with error codes (`CertificateException`, `SynchronizationException`, `ManifestationException`, `ConfigurationException`, `ValidationException`)
- **Error Codes**: Enum per category (`CertificateErrorCode`, `SyncErrorCode`, `ManifestationErrorCode`)
- **Context Preservation**: All exceptions carry `Cnpj?`, `Nsu?`, `ChaveAcesso?` when applicable
- **Retry Policy**: Only for explicitly classified transient errors (429, 5xx, network, timeout)
- **No Swallowing**: Exceptions propagate; caught at use case boundary for result wrapping

## Validation
- **Value Objects**: Validate in factory/constructor (`Cnpj.Parse`, `ChaveAcesso.Parse`, `Nsu` ctor)
- **Entities**: Validate in factory methods (`Empresa.Create`)
- **Configuration**: Startup validation via `AddAppSettingsValidation` (required folders)
- **Pre-flight**: `ValidateEmpresaReadyUseCase` before sync

## Imports & Using
- Implicit usings enabled — minimal explicit `using` statements
- `System` namespace implicitly available
- Project-specific namespaces explicit

## WPF / MVVM
- **ViewModel**: Implements `INotifyPropertyChanged` (manual `SetField` helper)
- **View**: Code-behind only for `InitializeComponent` and simple event forwarding
- **Controls**: Self-contained `UserControl` (e.g. `DynamicIslandControl`) owns its animation code-behind; animations are serialized to avoid racing storyboards
- **Notifications**: ViewModel raises `IIslandNotifier.Notify(IslandNotification)`; never references island controls
- **Commands**: `RelayCommand` (sync) / `AsyncRelayCommand` (async with execution guard)
- **No UI Access**: ViewModel never touches visual controls directly
- **Progress Reporting**: `IProgressReporter` interface implemented by ViewModel
- **Thread Safety**: Long operations on background; UI updates via `PropertyChanged` on UI thread
- **Boot**: Splash shown before host build with 800 ms minimum; `ShutdownMode` switches to `OnMainWindowClose` only after MainWindow is assigned

## Configuration
- Strongly-typed `IOptions<>` pattern
- Settings classes in `NEO-e.Infrastructure.Configuration` with `SectionName` constants
- Validation via `OptionsBuilder.ValidateOnStart()`
- Environment-specific URLs in settings, not code

## Logging
- **Structured**: Serilog with message templates (`{Property}`)
- **No PII/Fiscal Data**: XML content, passwords, tokens, private keys never logged
- **Levels**: Debug for HTTP details, Information for flow, Warning for retries, Error for failures
- **Source Context**: Automatic via `ForContext<T>()`

## Database (SQLite)
- **Schema**: Explicit `CREATE TABLE` in `EnsureInitializedAsync`
- **Migrations**: Not used (simple schema, additive changes only)
- **Concurrency**: `SemaphoreSlim` for init; single connection per operation
- **Parameters**: Always parameterized (`$cnpj`, `$chave`)
- **Dates**: Stored as ISO 8601 (`ToString("O")`)
- **Enums**: Stored as string (`.ToString()`)

## File System
- **Atomic Writes**: Temp file (`.guid.tmp`) → `File.Move` (atomic on same volume)
- **Idempotency**: Hash compare before overwrite; different content throws
- **Structure**: `DestinationPath/CNPJ/tipo/year/month/chave.xml` (configurable)
- **Cleanup**: Temp file deleted in `finally` block

## Testing
- **Framework**: xUnit (test project references `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`)
- **Naming**: `ClassName_MethodName_Scenario` or descriptive facts
- **Arrange/Act/Assert**: Explicit structure
- **No External Dependencies**: Handlers/fakes for HTTP, file system, certificates
- **Current Coverage**: 8 tests passing (NSU monotonicity, GZip parsing, idempotent write, SQLite persistence)

## Security Conventions
- **No Secrets in Code**: Connection strings, passwords, tokens only via config/environment
- **Certificate Password**: Never persisted; held in `CertificateRowViewModel.Password` (memory only during session)
- **Log Sanitization**: Fiscal XML excluded via Serilog filter (override for `AdnHttpClient` = Debug)
- **Production Gate**: `EnvironmentContext` throws on unconfirmed switch to Producao

## Git / Commits
- Conventional Commits (implied by AGENTS.md)
- Small, coherent commits
- No emojis in commits, code, or docs (per AGENTS.md)

## Evidence
- All `.csproj` files: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
- `src/NEO-e.Domain/ValueObjects/Identifiers.cs` (value object patterns)
- `src/NEO-e.Domain/Exceptions/DomainException.cs` (typed exceptions)
- `src/NEO-e.Application/UseCases/SyncUseCases.cs` (async + cancellation)
- `src/NEO-e.Infrastructure/Http/AdnHttpClient.cs` (retry, logging)
- `src/NEO-e.Infrastructure/Persistence/*.cs` (SQLite patterns)
- `src/NEO-e.Infrastructure/FileSystem/AtomicXmlFileWriter.cs` (atomic write)
- `src/NEO-e.App/ViewModels/MainWindowViewModel.cs` (MVVM, commands, progress)
- `src/NEO-e.App/Commands/RelayCommand.cs` (command patterns)
- `tests/NEO-e.UnitTests/UnitTest1.cs` (test structure)