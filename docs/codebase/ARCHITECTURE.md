# Architecture

## Architectural Style
**Clean Architecture** with explicit layer boundaries:
- **Domain** (innermost) — Pure business logic, no external dependencies
- **Application** — Use cases orchestrating domain, depends on Domain + Contracts (interfaces)
- **Contracts** — External-facing DTOs and interfaces, depends on Domain
- **Infrastructure** (outermost) — Adapters implementing Application/Contracts interfaces
- **App** (WPF) — Composition root, UI layer, depends on all inner layers

## Dependency Rule
```
App → Infrastructure → Application → Domain
              ↑              ↑
           Contracts ──────┘
```
- Domain has **zero** external dependencies
- Application depends only on Domain + Contracts (interfaces)
- Infrastructure implements interfaces from Application/Contracts
- App composes everything via DI

## Layer Responsibilities

### Domain (`NEO-e.Domain`)
| Type | Responsibility |
|------|----------------|
| `Empresa` | Aggregate root: CNPJ, certificate info, NSU, sync status, selection, password state |
| `DocumentoFiscal` | Immutable fiscal document: chave, tipo, CNPJs, dates, valor, XML, hash, NSU |
| `EstadoSincronizacao` | Per-CNPJ sync cursor: ultimo NSU confirmado/consultado, max NSU, version |
| `Cnpj` / `Nsu` / `ChaveAcesso` / `ValorMonetario` | Strongly-typed value objects with validation |
| `*Exception` / `*ErrorCode` | Typed errors preserving context (CNPJ, NSU, error category) |

**Key Invariants**:
- NSU never regresses (`Empresa.AtualizarNsu` throws on regression)
- Document hash computed on creation for idempotency
- `Empresa.EstaProntaParaSincronizar` encapsulates all preconditions

### Application (`NEO-e.Application`)
| Use Case | Responsibility |
|----------|----------------|
| `SincronizarEmpresaUseCase` | Single-CNPJ sync loop: fetch batch → parse → dedupe → write → persist → confirm NSU |
| `SincronizarCarteiraUseCase` | Orchestrates multiple companies sequentially with progress reporting |
| `DiscoverCertificatesUseCase` | Scans folder for `.pfx`/`.p12`, extracts metadata |
| `LoadCertificateUseCase` | Loads X509Certificate2 with password (MachineKeySet + Exportable) |
| `ValidateCertificateCnpjUseCase` | Validates private key, validity, CNPJ match |
| `ValidateEmpresaReadyUseCase` | Pre-flight check: selected, password, cert valid, not expired |

**Application Contracts (Interfaces)** — Defined in `Interfaces.cs`:
- `ICertificateRepository`, `INsuRepository`, `IDocumentRepository`
- `IAdnClient` (HTTP), `IXmlParser`, `IFileWriter`
- `IProgressReporter` (UI callbacks), `IGapAnalyzer`, `IExcelExporter`

### Infrastructure (`NEO-e.Infrastructure`)
| Adapter | Interface | Key Details |
|---------|-----------|-------------|
| `AdnHttpClient` | `IAdnClient` | mTLS per certificate (cached `HttpClient`), retry with jitter + `Retry-After`, timeout, rate limit config |
| `CertificateRepository` | `ICertificateRepository` | Discovers certs without password; loads with password; validates CNPJ from Subject/SERIALNUMBER |
| `SqliteNsuRepository` | `INsuRepository` | SQLite table `estado_sincronizacao` with UPSERT, version for optimistic concurrency |
| `SqliteDocumentRepository` | `IDocumentRepository` | SQLite table `documentos` with PK on `chave`, indexes on destinatario+data |
| `NfseXmlParser` | `IXmlParser` | Base64 → GZip → XML → XDocument; extracts metadata via flexible name matching |
| `AtomicXmlFileWriter` | `IFileWriter` | Write to `.tmp` → atomic move; idempotent (hash compare); folder structure by CNPJ/tipo/year/month |
| `SerilogLogger` | `ILogger` | Wrapper over Serilog with structured logging; fiscal payload excluded |

### App (`NEO-e.App`)
- **Composition Root**: `App.xaml.cs` builds `IHost`, registers all services, starts host
- **ViewModel**: `MainWindowViewModel` implements `IProgressReporter` + `INotifyPropertyChanged`
- **Commands**: `RelayCommand` / `AsyncRelayCommand` with `CanExecute` guards
- **UI State**: Certificate grid (search, select all, password per row), environment selector (blocked Prod), folder config, sync controls

## Data Flow: Sincronização
```
User clicks "Sincronizar"
    │
    ▼
MainWindowViewModel.StartSyncAsync()
    │
    ▼
SincronizarCarteiraUseCase.ExecuteAsync(empresas, getCert, resetNsu, ct)
    │
    ├─► For each selected Empresa:
    │       │
    │       ▼
    │   SincronizarEmpresaUseCase.ExecuteAsync(empresa, cert, resetNsu, ct)
    │       │
    │       ├─► INsuRepository.GetAsync(cnpj) → EstadoSincronizacao
    │       │
    │       ├─► Loop while not cancelled:
    │       │       │
    │       │       ▼
    │       │   IAdnClient.GetDfeAsync(cnpj, ultimoNsuConfirmado, cert, ct)
    │       │       │
    │       │       ▼
    │       │   IXmlParser.ParseLote(response.Lote) → DocumentoFiscal[]
    │       │       │
    │       │       ├─► IDocumentRepository.GetByChaveAsync(chave) → dedupe
    │       │       │
    │       │       ├─► IFileWriter.WriteAsync(doc) → atomic XML file
    │       │       │
    │       │       ├─► IDocumentRepository.SaveAsync(doc)
    │       │       │
    │       │       ▼
    │       │   EstadoSincronizacao.AtualizarProgresso / ConfirmarNsu
    │       │       │
    │       │       ▼
    │       │   INsuRepository.SaveAsync(estado)
    │       │       │
    │       │       ▼ (loop until UltNsu >= MaxNsu or empty batch)
    │       │
    │       ▼
    │   Empresa.AtualizarNsu / AtualizarStatus
    │
    ▼
CarteiraSyncResult (aggregated)
```

## Cross-Cutting Concerns

### Configuration
- `IOptions<AppSettings>` bound from `appsettings.json` section `App`
- Validation on startup via `AddAppSettingsValidation` (required folders)
- `IEnvironmentContext` — runtime environment switching (Restrita/Producao) with explicit confirmation gate

### Logging
- Serilog with file sink (daily rolling, 31 days retention, 10MB max)
- Structured template: timestamp, level, source context, message, exception
- `AdnHttpClient` logs at Debug level; fiscal XML **never** logged

### Cancellation & Progress
- `CancellationToken` propagated through all async operations
- `IProgressReporter` implemented by ViewModel for real-time UI updates
- Cooperative cancellation: `ct.ThrowIfCancellationRequested()` at loop boundaries

### Security
- Certificates: `MachineKeySet | Exportable`; password **only in memory** during batch
- mTLS: One `HttpClient` per certificate thumbprint (cached in `ConcurrentDictionary`)
- Production environment: Requires explicit confirmation via `EnvironmentContext.SetEnvironment`
- No secrets in logs, config files, or ViewModel persistence

## Concurrency Model
- **SQLite**: Single-writer per repository (serialized by connection), `SemaphoreSlim` for init
- **Sync Loop**: Sequential per company (`SincronizarCarteiraUseCase` processes one at a time)
- **HttpClient**: Reused per certificate (thread-safe `SocketsHttpHandler`)
- **UI**: All long-running ops on background threads; `AsyncRelayCommand` prevents re-entry

## Extension Points (Interfaces Not Yet Implemented)
- `IGapAnalyzer` — Detect/recover missing NSU ranges
- `IExcelExporter` — Export execution logs and document inventory
- `Eventos` endpoint in `IAdnClient` (stubbed, returns null on 404)

## Evidence
- `src/NEO-e.Domain/Entities/Empresa.cs` lines 63-74 (NSU monotonicity)
- `src/NEO-e.Application/UseCases/SyncUseCases.cs` lines 34-168 (sync loop)
- `src/NEO-e.Application/Contracts/Interfaces.cs` (all interfaces)
- `src/NEO-e.Infrastructure/Http/AdnHttpClient.cs` lines 30-149 (HTTP with retry)
- `src/NEO-e.Infrastructure/Persistence/SqliteNsuRepository.cs` lines 97-136 (UPSERT with version)
- `src/NEO-e.Infrastructure/FileSystem/AtomicXmlFileWriter.cs` lines 15-39 (atomic write)
- `src/NEO-e.App/App.xaml.cs` lines 18-43 (DI composition)
- `src/NEO-e.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` lines 18-51 (registration)