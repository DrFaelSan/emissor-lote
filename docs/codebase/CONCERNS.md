# Concerns / Technical Debt / Risks

## Critical Risks (Blocking Production)

### 1. ADN API Contract Not Validated
**Severity**: Critical
**Status**: Open
**Impact**: Cannot homologate or deploy to production without confirmed contract
**Details**:
- Endpoint paths, request/response schemas, NSU semantics (`ultNSU` vs `ultimoNsu` vs `maxNSU`) assumed from partial documentation
- No sanitized fixtures for testing
- No authorized smoke test in Restrita environment
- Tasks: `TASK-EPIC-00-API-ADN.md` API-001 through API-005

**Evidence**: `tasks/TASK-EPIC-00-API-ADN.md`, `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` lines 52-70

### 2. Certificate CNPJ Extraction Heuristic
**Severity**: High
**Status**: Open
**Impact**: Certificates may fail validation if Subject format differs from assumed `CNPJ=` or `SERIALNUMBER=` patterns
**Details**: `CertificateRepository.ExtractCnpjFromCertificate` uses regex on Subject DN only. Official ICD/ABRASF spec not confirmed.
**Location**: `src/NEO-e.Infrastructure/Certificates/CertificateRepository.cs` lines 110-123

### 3. Password Storage Policy Undefined
**Severity**: High
**Status**: Open (EPIC 1 - CORE-003)
**Impact**: Passwords currently held only in ViewModel memory during session; no persistence strategy for UX across restarts
**Options**: DPAPI (user/machine scope) or Windows Credential Manager
**Requirement**: Never in JSON, logs, or ViewModel persistence

**Evidence**: `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` lines 32-36, `AGENTS.md` line 67

## Technical Debt

### 4. SQLite Corruption Detection/Backup Missing
**Severity**: Medium
**Status**: Open (EPIC 4 pending)
**Impact**: Corrupted `state.db` could lose NSU state → duplicate downloads or gaps
**Needed**: Integrity check on open (`PRAGMA integrity_check`), automated backup before write, recovery path
**Location**: `SqliteNsuRepository`, `SqliteDocumentRepository`

**Evidence**: `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` line 89

### 5. NSU Semantics Unconfirmed
**Severity**: Medium
**Status**: Open (EPIC 4 pending)
**Impact**: `ultimoNsu`, `ultNSU`, `maxNSU` mapping may be wrong → infinite loop or missed documents
**Needed**: Confirm with official ADN contract
**Location**: `AdnHttpClient.MapToResponse`, `EstadoSincronizacao`

**Evidence**: `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` line 90

### 6. Eventos Endpoint Not Integrated
**Severity**: Low-Medium
**Status**: Stubbed
**Impact**: Event download (cancellation, substitution) not in sync loop
**Details**: `IAdnClient.GetEventosAsync` implemented but returns null on 404; not called in `SincronizarEmpresaUseCase`
**Location**: `src/NEO-e.Application/Contracts/Interfaces.cs` line 59, `src/NEO-e.Infrastructure/Http/AdnHttpClient.cs` lines 56-75

### 7. Gap Analyzer & Exporter Not Implemented
**Severity**: Low
**Status**: Interfaces only
**Impact**: No NSU gap detection/recovery; no Excel export for audit
**Interfaces**: `IGapAnalyzer`, `IExcelExporter` in `Interfaces.cs`

### 8. Batch Size vs Rate Limit Tuning
**Severity**: Low
**Status**: Defaults only
**Impact**: `MaxBatchSize=50`, `RateLimitPerMinute=300` may need adjustment per environment
**Location**: `AdnSettings` in `appsettings.json`

## Architecture Concerns

### 9. Sequential Carteira Sync (No Parallelism)
**Severity**: Low (MVP)
**Status**: By design
**Impact**: Slow for many companies; `SincronizarCarteiraUseCase` processes one at a time
**Future**: Parallel with semaphore per certificate, respecting rate limits

### 10. No Database Migrations
**Severity**: Low
**Status**: Additive schema only
**Impact**: Schema changes require manual SQL or recreate; acceptable for embedded SQLite MVP

### 11. Single `state.db` for All Companies
**Severity**: Low
**Status**: Current design
**Impact**: All CNPJs share one file; corruption affects all. Could split per CNPJ if needed.

## Security Concerns

### 12. Certificate Password in Memory Only
**Severity**: Medium (by design)
**Status**: Intentional for MVP
**Impact**: User must re-enter password each session; UX friction vs security trade-off
**Mitigation**: Implement DPAPI/Credential Manager per CORE-003

### 13. Log Sanitization Verification Needed
**Severity**: Low
**Status**: Configured but not tested
**Needed**: Automated test verifying no fiscal XML, password, token, private key in log output
**Location**: `Serilog` config in `ServiceCollectionExtensions.AddSerilog`

### 14. Production Environment Gate
**Severity**: Medium (safety feature)
**Status**: Implemented
**Details**: `EnvironmentContext.SetEnvironment` throws if switching to Producao without explicit confirmation. UI must implement two-step confirmation.

**Evidence**: `src/NEO-e.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` lines 151-159

## Testing Gaps

### 15. Visible Test Project Empty
**Severity**: Medium
**Status**: `tests/NEO-e.UnitTests/UnitTest1.cs` has only empty test
**Impact**: Cannot verify the 8 passing tests claimed in documentation
**Action**: Locate actual test files or recreate test coverage

### 16. No Integration Test Infrastructure
**Severity**: Medium
**Status**: No fake handlers, no fixtures
**Needed**: `FakeHttpMessageHandler`, sanitized certificate PFX, ADN response fixtures

## Performance Concerns

### 17. XML Parsing Allocation
**Severity**: Low
**Status**: `XDocument.Parse` per document; Base64→GZip→MemoryStream allocations
**Impact**: Acceptable for current batch sizes; could optimize with `XmlReader` streaming if volume increases

### 18. SQLite Connection Per Operation
**Severity**: Low
**Status**: New connection each call (no pooling for SQLite)
**Impact**: Acceptable for low concurrency; `Pooling=false` in connection string

## Documentation Gaps

### 19. No Official Architecture Decision Records (ADRs)
**Severity**: Low
**Status**: Decisions embedded in code and task files
**Needed**: ADRs for: Clean Architecture choice, SQLite over PostgreSQL, sequential sync, password policy, environment gate

### 20. API Contract Documentation Missing
**Severity**: Critical (see #1)
**Status**: `docs/api-adn.md` not created
**Needed**: Per API-001: register manual version, Swagger URL, OpenAPI hash, Restrita/Producao URLs

## High-Churn Files (Git History Not Available)
**Note**: No git commits yet (`fatal: your current branch 'main' does not have any commits yet`). Cannot identify high-churn files. Will be relevant after first commits.

**Predicted High-Churn** (based on complexity):
- `SyncUseCases.cs` — core business logic
- `AdnHttpClient.cs` — external integration, retry logic
- `MainWindowViewModel.cs` — UI orchestration, 572 lines
- `NfseXmlParser.cs` — schema-dependent parsing

## Evidence
- `docs/ESTADO-ATUAL-E-PROXIMO-EPIC.md` (all pending items per EPIC)
- `tasks/TASK-EPIC-00-API-ADN.md` (contract validation tasks)
- `tasks/TASK-EPIC-01-CORE.md` (CORE-003 password policy)
- `tasks/TASK-EPIC-04-SINCRONIZACAO.md` (referenced, not read)
- `src/NEO-e.Infrastructure/Certificates/CertificateRepository.cs` lines 110-123
- `src/NEO-e.Infrastructure/Http/AdnHttpClient.cs` lines 192-217 (NSU mapping)
- `src/NEO-e.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` lines 151-159 (Prod gate)
- `src/NEO-e.App/ViewModels/MainWindowViewModel.cs` (572 lines, UI orchestration)