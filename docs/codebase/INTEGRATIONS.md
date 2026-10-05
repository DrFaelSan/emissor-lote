# Integrations

## External Systems

### 1. ADN API (NFSe Nacional)
**Purpose**: Consulta de DFe (Documentos Fiscais Eletrônicos) e Eventos por NSU

| Aspect | Detail |
|--------|--------|
| **Protocol** | HTTPS + mTLS (client certificate) |
| **Base URLs** | `Restrita`: `https://adn.producaorestrita.nfse.gov.br/`<br>`Producao`: `https://adn.nfse.gov.br/` |
| **Authentication** | X.509 A1 certificate (PFX/P12) with private key, CNPJ in Subject/SERIALNUMBER |
| **Endpoints** | `GET contribuinte/DFe/{nsu}?cnpjConsulta={cnpj}`<br>`GET contribuinte/NFSe/{chave}/Eventos` |
| **Response Format** | JSON with Base64-GZip compressed XML payload |
| **Pagination** | Cursor-based via NSU (`ultNSU`, `maxNSU`) |
| **Rate Limit** | Configurable (default 300 req/min) |
| **Retry Policy** | Exponential backoff + jitter, respects `Retry-After`, max 3 attempts, retryable: 429, 500, 502, 503, 504 |
| **Timeout** | 60 seconds (configurable) |
| **Contract Status** | **NOT YET VALIDATED** — requires official spec, sanitized fixtures, smoke test in Restrita |

**Implementation**: `AdnHttpClient` (`NEO-e.Infrastructure.Http`)
- Caches `HttpClient` per certificate thumbprint (`ConcurrentDictionary`)
- `SocketsHttpHandler` with `SslClientAuthenticationOptions.ClientCertificates`
- Structured logging (Debug level), fiscal payload excluded

**Configuration** (`AdnSettings` in `appsettings.json`):
```json
"Adn": {
  "BaseUrlRestrita": "https://adn.producaorestrita.nfse.gov.br/",
  "BaseUrlProducao": "https://adn.nfse.gov.br/",
  "TimeoutSeconds": 60,
  "MaxBatchSize": 50,
  "RateLimitPerMinute": 300,
  "Retry": { "MaxAttempts": 3, "BaseDelayMs": 500, "MaxDelayMs": 10000, "BackoffMultiplier": 2.0, "RespectRetryAfter": true, "RetryableStatusCodes": [429,500,502,503,504] }
}
```

### 2. Local File System (Certificates)
**Purpose**: Discover and load A1 certificates (PFX/P12)

| Aspect | Detail |
|--------|--------|
| **Location** | Configurable folder (default `C:\Certificados`) |
| **Extensions** | `.pfx`, `.p12` |
| **Discovery** | Reads metadata without password (Subject, Issuer, Validity, Thumbprint, HasPrivateKey) |
| **Loading** | Requires password; `X509KeyStorageFlags.MachineKeySet \| Exportable` |
| **Validation** | Private key present, not expired, not not-yet-valid, CNPJ matches expected |

**Implementation**: `CertificateRepository` (`NEO-e.Infrastructure.Certificates`)

### 3. Local File System (XML Storage)
**Purpose**: Persist downloaded NFSe XML files

| Aspect | Detail |
|--------|--------|
| **Root Path** | Configurable (default `C:\NotasFiscais`) |
| **Structure** | `CNPJ/tipo/year/month/chave.xml` (configurable: Flat, YearMonth, YearMonthType, TypeYearMonth) |
| **Write Pattern** | Atomic: `.tmp` → `File.Move` |
| **Idempotency** | SHA-256 hash compare; identical = skip; different = throw |
| **Read/List** | By CNPJ, tipo, date range |

**Implementation**: `AtomicXmlFileWriter` (`NEO-e.Infrastructure.FileSystem`)

### 4. SQLite (Embedded Database)
**Purpose**: Persist sync state (NSU) and document metadata

| Aspect | Detail |
|--------|--------|
| **File Location** | `{DestinationPath}/.neo-e/state.db` |
| **Tables** | `estado_sincronizacao` (PK: cnpj), `documentos` (PK: chave) |
| **Indexes** | `idx_estado_ultima_sync` on `ultima_sincronizacao`<br>`idx_documentos_destinatario_data` on `cnpj_destinatario, data_emissao` |
| **Concurrency** | Single-writer per repo, `SemaphoreSlim` for init |
| **Migrations** | None (additive schema only, `CREATE TABLE IF NOT EXISTS`) |

**Implementation**: `SqliteNsuRepository`, `SqliteDocumentRepository` (`NEO-e.Infrastructure.Persistence`)

### 5. Logging (Serilog)
**Purpose**: Structured application logging

| Aspect | Detail |
|--------|--------|
| **Sink** | File (daily rolling) |
| **Path** | `logs/neo-e-.log` (relative to base directory) |
| **Retention** | 31 files |
| **Max File Size** | 10 MB |
| **Template** | `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}` |
| **Levels** | Default: Information; Microsoft/System: Warning; AdnHttpClient: Debug |
| **Sanitization** | Fiscal XML never logged; password/token/key never logged |

**Configuration** (`LoggingSettings` + Serilog section in `appsettings.json`)

## Internal Integrations (DI Composition)

| Consumer | Provider | Interface | Lifetime |
|----------|----------|-----------|----------|
| `SincronizarEmpresaUseCase` | `AdnHttpClient` | `IAdnClient` | Singleton |
| `SincronizarEmpresaUseCase` | `SqliteNsuRepository` | `INsuRepository` | Singleton |
| `SincronizarEmpresaUseCase` | `SqliteDocumentRepository` | `IDocumentRepository` | Singleton |
| `SincronizarEmpresaUseCase` | `NfseXmlParser` | `IXmlParser` | Singleton |
| `SincronizarEmpresaUseCase` | `AtomicXmlFileWriter` | `IFileWriter` | Singleton |
| `SincronizarEmpresaUseCase` | `MainWindowViewModel` | `IProgressReporter` | Singleton |
| `DiscoverCertificatesUseCase` | `CertificateRepository` | `ICertificateRepository` | Singleton |
| `LoadCertificateUseCase` | `CertificateRepository` | `ICertificateRepository` | Singleton |
| `ValidateCertificateCnpjUseCase` | `CertificateRepository` | `ICertificateRepository` | Singleton |
| `AdnHttpClient` | `EnvironmentContext` | `IEnvironmentContext` | Singleton |
| All | `SerilogLogger` | `ILogger` | Singleton |

**Composition Root**: `App.xaml.cs` → `Host.CreateDefaultBuilder()` → `AddInfrastructure()` → `Build()` → `Start()`

## Environment Management

| Environment | Base URL | Confirmation Required |
|-------------|----------|----------------------|
| `Restrita` (Homologation) | `adn.producaorestrita.nfse.gov.br` | No |
| `Producao` | `adn.nfse.gov.br` | **Yes** — explicit two-step confirmation via UI |

**Implementation**: `EnvironmentContext` (`NEO-e.Infrastructure.DependencyInjection`)
- `Current` property, `BaseUrl` computed property
- `SetEnvironment(EnvironmentType)` throws `InvalidOperationException` if switching to Producao without confirmation
- Event `EnvironmentChanged` notifies UI

## Pending / Planned Integrations

| Integration | Status | Notes |
|-------------|--------|-------|
| **Eventos endpoint** | Stubbed | `IAdnClient.GetEventosAsync` returns null on 404; not used in sync loop |
| **Gap Analyzer** | Interface only | `IGapAnalyzer.AnalyzeAsync` / `RecoverAsync` for NSU gap detection |
| **Excel Exporter** | Interface only | `IExcelExporter.ExportExecutionAsync` / `ExportInventoryAsync` for reports |
| **Windows Credential Manager / DPAPI** | Not implemented | Password storage policy defined in EPIC 1 (CORE-003) — pending decision |
| **Official ADN Contract Fixtures** | Not created | Task API-001 to API-005 in `TASK-EPIC-00-API-ADN.md` |

## Security Boundaries

| Boundary | Protection |
|----------|------------|
| **Certificate Password** | Memory-only during batch; never in config, logs, ViewModel persistence |
| **Private Key** | `MachineKeySet` (machine store); `Exportable` for in-memory use |
| **Production API** | Explicit confirmation gate in `EnvironmentContext` |
| **Log Output** | Fiscal XML excluded; structured templates prevent accidental serialization |
| **SQLite** | Local file only; no network exposure |

## Evidence
- `src/NEO-e.App/appsettings.json` (all integration config)
- `src/NEO-e.Infrastructure/Http/AdnHttpClient.cs` (ADN client)
- `src/NEO-e.Infrastructure/Certificates/CertificateRepository.cs` (certificate FS)
- `src/NEO-e.Infrastructure/FileSystem/AtomicXmlFileWriter.cs` (XML storage)
- `src/NEO-e.Infrastructure/Persistence/*.cs` (SQLite)
- `src/NEO-e.Infrastructure/Logging/Logger.cs` (Serilog wrapper)
- `src/NEO-e.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` (DI registration)
- `src/NEO-e.App/App.xaml.cs` (composition root)
- `tasks/TASK-EPIC-00-API-ADN.md` (contract validation tasks)