# Testing

## Test Framework
- **xUnit** (v2.4+) via `xunit`, `xunit.runner.visualstudio`
- **Test Project**: `tests/NEO-e.UnitTests/NEO-e.UnitTests.csproj` (net8.0)
- **Current Tests**: 8 passing (as of 2026-09-23)

## Test Organization
```
tests/NEO-e.UnitTests/
├── UnitTest1.cs          # Placeholder (empty test)
└── (domain/logic tests implied by documentation)
```

**Note**: The current `UnitTest1.cs` contains only an empty `Test1` fact. The 8 passing tests referenced in `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` are likely in other test files not yet visible in the workspace, or were run from a different test project configuration.

## Covered Scenarios (Documented)
Per `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` validation evidence:
1. **NSU negativo rejeitado** — `Nsu` constructor throws on negative value
2. **Monotonicidade do estado de sincronizacao** — `EstadoSincronizacao` rejects regression
3. **Decodificacao GZip e parsing de XML com namespace** — `NfseXmlParser.DecodeXml` + `ParseLote`
4. **Escrita idempotente e limpeza do arquivo temporario** — `AtomicXmlFileWriter.WriteAsync`
5. **Persistencia e leitura de documento no SQLite** — `SqliteDocumentRepository.SaveAsync` / `GetByChaveAsync`

## Required Test Categories (Per AGENTS.md & Architecture)

### Unit Tests (Domain/Application)
| Area | Required Coverage |
|------|-------------------|
| Value Objects | `Cnpj` validation (valid, invalid length, invalid check digit), `Nsu` (zero, negative, comparison), `ChaveAcesso` (length, CNPJ extraction), `ValorMonetario` (rounding, operators) |
| Entities | `Empresa` (NSU monotonicity, status transitions, `EstaProntaParaSincronizacao`), `DocumentoFiscal` (hash, equality), `EstadoSincronizacao` (progress, confirmation, `CaixaVazia`, `PrecisaSincronizar`) |
| Exceptions | All error codes serialized correctly, context preserved |
| Use Cases | `SincronizarEmpresaUseCase` (empty batch, dedupe, write failure → no NSU confirm, cancellation), `SincronizarCarteiraUseCase` (sequential, cert missing, aggregation), Certificate use cases (discover, load, validate) |

### Integration Tests (Infrastructure)
| Adapter | Required Coverage |
|---------|-------------------|
| `AdnHttpClient` | 400, 401, 403, 429, 5xx, timeout, invalid JSON, `Retry-After` respected, cert per client cache |
| `CertificateRepository` | Valid PFX, invalid password, expired, no private key, CNPJ mismatch, CNPJ extraction from Subject/SERIALNUMBER |
| `SqliteNsuRepository` | UPSERT, version increment, GetAll, Delete, concurrent init safety |
| `SqliteDocumentRepository` | Save/Get by chave, by CNPJ, by period, batch, PK conflict handling |
| `NfseXmlParser` | Valid NFSe XML, missing fields, multiple name variants, Base64+GZip decode, malformed XML |
| `AtomicXmlFileWriter` | Atomic write, idempotent skip, different content throws, folder structure variants, cleanup on error |

### End-to-End / Contract Tests
- **ADN API Contract**: Fake `HttpMessageHandler` with sanitized fixtures (success, empty, 401, 403, 429, 500, invalid payload)
- **Certificate Flow**: Test with controlled test certificates (sanitized fixtures)
- **Sync Loop**: Full flow from NSU=0 to MaxNSU, with cancellation at each step
- **Environment Switch**: Restrita ↔ Producao with confirmation gate

## Mocking Strategy
- **No External Dependencies in Tests**: All I/O abstracted behind interfaces
- **Handlers/Fakes Preferred**: `HttpMessageHandler` fake for HTTP, in-memory SQLite (`:memory:`) or temp file for DB, temp directory for file system
- **Test Certificates**: Sanitized PFX fixtures (no real private keys in repo)

## Test Conventions
- **Naming**: `MethodName_Scenario_ExpectedResult` or descriptive `Fact` names
- **Structure**: Arrange / Act / Assert explicit
- **Async**: All async tests use `async Task` with `CancellationToken`
- **Isolation**: Each test independent; no shared state
- **Deterministic**: No `DateTime.Now` in tests — inject clock or use fixed values

## CI / Automation
- **Commands**: `dotnet test NEO-e.slnx --no-restore`
- **Current Status**: 8 tests passing (baseline)
- **Target**: All new domain rules require unit test; all external integrations require fake-based integration test

## Gaps / TODO
- [ ] No test project visible with the 8 passing tests (only empty `UnitTest1.cs`)
- [ ] No fake `HttpMessageHandler` for `AdnHttpClient` tests
- [ ] No sanitized certificate fixtures
- [ ] No ADN API contract fixtures (Task API-005)
- [ ] No gap analyzer / exporter tests (interfaces not implemented)
- [ ] No WPF UI tests (ViewModel logic testable via `IProgressReporter` mock)

## Evidence
- `tests/NEO-e.UnitTests/NEO-e.UnitTests.csproj` (project config)
- `tests/NEO-e.UnitTests/UnitTest1.cs` (current placeholder)
- `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` lines 96-109 (validation evidence)
- `src/NEO-e.Application/Contracts/Interfaces.cs` (all interfaces for mocking)
- `AGENTS.md` lines 62-68 (test requirements)