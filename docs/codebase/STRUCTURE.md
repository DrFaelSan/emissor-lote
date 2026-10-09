# Project Structure

## Root Layout
```
NEO-e/
├── AGENTS.md                          # Agent instructions for this repo
├── docs/
│   ├── ESTADO-ATUAL-E-PROXIMO-EPIC.md # State tracking document
│   └── codebase/                      # This documentation (generated)
├── src/
│   ├── NEO-e.App/                     # WPF Application (entry point)
│   ├── NEO-e.Application/             # Application layer (use cases)
│   ├── NEO-e.Contracts/               # External contracts (DTOs)
│   ├── NEO-e.Domain/                  # Domain layer (entities, VOs, exceptions)
│   └── NEO-e.Infrastructure/          # Infrastructure adapters
├── tasks/                             # Task breakdowns and RFCs
└── tests/
    └── NEO-e.UnitTests/               # Unit test project (xUnit)
```

## Source Code Organization

### NEO-e.Domain (`src/NEO-e.Domain/`)
```
Entities/
  ├── Empresa.cs              # Company aggregate root
  ├── DocumentoFiscal.cs      # Fiscal document entity
  └── EstadoSincronizacao.cs  # Sync state per CNPJ
ValueObjects/
  └── Identifiers.cs          # CNPJ, NSU, ChaveAcesso, ValorMonetario
Exceptions/
  └── DomainException.cs      # CertificateException, SynchronizationException, ConfigurationException, ManifestationException, ValidationException
```

### NEO-e.Application (`src/NEO-e.Application/`)
```
UseCases/
  ├── SyncUseCases.cs         # SincronizarEmpresaUseCase, SincronizarCarteiraUseCase
  └── CertificateUseCases.cs  # Discover, Load, Validate, Update, ValidateReady
Contracts/
  └── Interfaces.cs           # All application interfaces (repository, client, parser, writer, progress, gap analyzer, exporter)
```

### NEO-e.Infrastructure (`src/NEO-e.Infrastructure/`)
```
Http/
  └── AdnHttpClient.cs        # IAdnClient implementation with mTLS, retry, rate limiting
Certificates/
  └── CertificateRepository.cs # ICertificateRepository implementation
Persistence/
  ├── SqliteNsuRepository.cs  # INsuRepository - SQLite
  └── SqliteDocumentRepository.cs # IDocumentRepository - SQLite
Parsers/
  └── NfseXmlParser.cs        # IXmlParser - Base64/GZip/NFSe XML parsing
FileSystem/
  └── AtomicXmlFileWriter.cs  # IFileWriter - Atomic writes with idempotency
DependencyInjection/
  └── ServiceCollectionExtensions.cs # AddInfrastructure, AddSerilog, AddAppSettingsValidation
Configuration/
  └── AppSettings.cs          # Strongly-typed settings classes
Logging/
  └── Logger.cs               # ILogger wrapper over Serilog
```

### NEO-e.Contracts (`src/NEO-e.Contracts/`)
- Currently minimal — DTOs for external API contracts (ADN response DTOs are internal in `AdnHttpClient.cs`)
- References Domain for shared value objects

### NEO-e.App (`src/NEO-e.App/`)
```
App.xaml / App.xaml.cs        # Application entry, DI composition, Host builder, splash boot
SplashScreenWindow.xaml / SplashScreenWindow.xaml.cs # Splash screen shown during host build
MainWindow.xaml / MainWindow.xaml.cs # Main window (code-behind minimal)
Controls/
  └── DynamicIslandControl.xaml / DynamicIslandControl.xaml.cs # Status island with hover/notification transitions
Services/
  ├── IslandNotification.cs   # IslandNotificationKind + IslandNotification record
  ├── IIslandNotifier.cs      # Notification event contract for the island
  └── IslandNotificationService.cs # Singleton notifier raised by the ViewModel
ViewModels/
  └── MainWindowViewModel.cs  # Main VM implementing IProgressReporter, INotifyPropertyChanged
Commands/
  ├── RelayCommand.cs         # Sync ICommand
  └── AsyncRelayCommand.cs    # Async ICommand with execution guard
appsettings.json              # Runtime configuration
AssemblyInfo.cs
```

## Key Entry Points
1. **Application**: `NEO-e.App.App` → `Host.CreateDefaultBuilder()` → DI → `MainWindow`
2. **Sync Flow**: `MainWindowViewModel.StartSyncAsync()` → `SincronizarCarteiraUseCase` → `SincronizarEmpresaUseCase` → `IAdnClient.GetDfeAsync()` → Parser → Writer → Repositories
3. **Certificate Flow**: `DiscoverCertificatesUseCase` → `CertificateRepository.DiscoverCertificatesAsync()` → UI binds to `CertificateRowViewModel`

## Configuration Files
- `src/NEO-e.App/appsettings.json` — All runtime settings (certificates, storage, ADN, environment, logging)
- No `appsettings.Development.json` — single config with environment switching via `EnvironmentSettings.Active`

## Evidence
- `src/NEO-e.Domain/` directory structure
- `src/NEO-e.Application/` directory structure
- `src/NEO-e.Infrastructure/` directory structure
- `src/NEO-e.Contracts/` directory structure
- `src/NEO-e.App/` directory structure
- `src/NEO-e.App/App.xaml.cs` lines 14-50 (DI composition)
- `src/NEO-e.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` lines 18-51 (Infrastructure registration)