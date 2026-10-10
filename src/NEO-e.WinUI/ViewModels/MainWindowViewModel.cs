using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using NEO_e.Application.Contracts;
using NEO_e.Application.UseCases;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;
using NEO_e.Infrastructure.Configuration;
using NEO_e.Infrastructure.DependencyInjection;
using NEO_e.WinUI.Commands;
using NEO_e.WinUI.Converters;
using NEO_e.WinUI.Services;

namespace NEO_e.WinUI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IProgressReporter
{
    private readonly DiscoverCertificatesUseCase _discoverCertificates;
    private readonly LoadCertificateUseCase _loadCertificate;
    private readonly Lazy<SincronizarCarteiraUseCase> _sincronizarCarteira;
    private readonly Lazy<SimularCarteiraUseCase> _simularCarteira;
    private readonly Lazy<ResetNsuUseCase> _resetNsuUseCase;
    private readonly IGapAnalyzer _gapAnalyzer;
    private readonly IExcelExporter _excelExporter;
    private readonly INsuRepository _nsuRepository;
    private readonly IReceivedDocumentIndexer _receivedDocumentIndexer;
    private readonly ICredentialManager _credentialManager;
    private readonly IAppSettingsProvider _settings;
    private readonly IEnvironmentContext _environment;
    private readonly IIslandNotifier _islandNotifier;
    private readonly PickerService _pickers;
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _discoveryCancellation;
    private CancellationTokenSource? _syncCancellation;
    private CancellationTokenSource? _operationCancellation;
    private string _certificateFolder;
    private string _destinationFolder;
    private string _selectedEnvironment;
    private string _selectedFolderStructure;
    private string _searchText = string.Empty;
    private string _statusMessage = "Pronto para configurar a sincronizacao.";
    private bool _isBusy;
    private bool _isSyncing;
    private bool _isSimulationMode;
    private bool _simulationFailureEnabled;
    private int _totalEmpresas;
    private int _empresasProcessadas;
    private int _totalDocumentos;
    private int _totalErros;
    private string _currentCnpj = string.Empty;
    private string _currentNsu = string.Empty;
    private bool _resetNsuConfirmed;
    private DateTimeOffset _syncStartedAt;
    private List<ExecutionRecord> _lastExecutionRecords = [];

    public MainWindowViewModel(
        DiscoverCertificatesUseCase discoverCertificates,
        LoadCertificateUseCase loadCertificate,
        Lazy<SincronizarCarteiraUseCase> sincronizarCarteira,
        Lazy<SimularCarteiraUseCase> simularCarteira,
        Lazy<ResetNsuUseCase> resetNsuUseCase,
        IGapAnalyzer gapAnalyzer,
        IExcelExporter excelExporter,
        INsuRepository nsuRepository,
        IReceivedDocumentIndexer receivedDocumentIndexer,
        ICredentialManager credentialManager,
        IAppSettingsProvider settings,
        IEnvironmentContext environment,
        IIslandNotifier islandNotifier,
        PickerService pickers,
        DispatcherQueue dispatcher)
    {
        _discoverCertificates = discoverCertificates;
        _loadCertificate = loadCertificate;
        _sincronizarCarteira = sincronizarCarteira;
        _simularCarteira = simularCarteira;
        _resetNsuUseCase = resetNsuUseCase;
        _gapAnalyzer = gapAnalyzer;
        _excelExporter = excelExporter;
        _nsuRepository = nsuRepository;
        _receivedDocumentIndexer = receivedDocumentIndexer;
        _credentialManager = credentialManager;
        _settings = settings;
        _environment = environment;
        _islandNotifier = islandNotifier;
        _pickers = pickers;
        _dispatcher = dispatcher;
        _certificateFolder = settings.Certificates.FolderPath;
        _destinationFolder = settings.Storage.DestinationPath;
        _selectedEnvironment = environment.Current.ToString();
        _selectedFolderStructure = settings.Storage.FolderStructure.ToString();

        ReloadCertificatesCommand = new AsyncRelayCommand(DiscoverCertificatesAsync, () => !IsBusy && !IsSyncing);
        ClearCertificatesCommand = new RelayCommand(ClearCertificates, () => !IsBusy && !IsSyncing && GetVisibleCertificates().Any());
        SelectAllCommand = new RelayCommand(SelectAllCertificates, () => !IsBusy && !IsSyncing && GetVisibleCertificates().Any());
        ClearSelectionCommand = new RelayCommand(ClearCertificateSelection, () => !IsBusy && !IsSyncing && GetVisibleCertificates().Any());
        ValidateConfigurationCommand = new RelayCommand(ValidateConfiguration, () => !IsBusy && !IsSyncing);
        StartSyncCommand = new AsyncRelayCommand(StartSyncAsync, () => !IsBusy && !IsSyncing && CanStartSync);
        CancelSyncCommand = new AsyncRelayCommand(CancelSyncAsync, () => IsSyncing || IsBusy);
        ResetNsuCommand = new AsyncRelayCommand(ResetNsuAsync, () => !IsBusy && !IsSyncing && CanResetNsu());
        AnalyzeGapsCommand = new AsyncRelayCommand(AnalyzeGapsAsync, () => !IsBusy && !IsSyncing && CanAnalyzeGaps());
        RecoverGapsCommand = new AsyncRelayCommand(RecoverGapsAsync, () => !IsBusy && !IsSyncing && CanRecoverGaps());
        ExportExecutionCommand = new AsyncRelayCommand(ExportExecutionAsync, () => !IsBusy && !IsSyncing);
        ExportInventoryCommand = new AsyncRelayCommand(ExportInventoryAsync, () => !IsBusy && !IsSyncing);
        IndexReceivedDocumentsCommand = new AsyncRelayCommand(IndexReceivedDocumentsAsync, () => !IsBusy && !IsSyncing);
        ChooseCertificateFolderCommand = new AsyncRelayCommand(ChooseCertificateFolderAsync, () => !IsBusy && !IsSyncing);
        ChooseDestinationFolderCommand = new AsyncRelayCommand(ChooseDestinationFolderAsync, () => !IsBusy && !IsSyncing);
        ToggleSimulationModeCommand = new RelayCommand(ToggleSimulationMode, () => !IsBusy && !IsSyncing);
        _environment.EnvironmentChanged += OnEnvironmentChanged;
    }

    public bool IsOperationRunning => IsBusy || IsSyncing;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CertificateRowViewModel> Certificates { get; } = [];
    public ObservableCollection<CertificateRowViewModel> VisibleCertificates { get; } = [];
    public ObservableCollection<LogMessage> LogMessages { get; } = [];

    public IReadOnlyList<string> Environments { get; } = ["Restrita", "Producao"];
    public IReadOnlyList<string> FolderStructures { get; } = ["Flat", "YearMonthType", "YearMonth", "TypeYearMonth"];
    public string AppVersion => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "Versao indisponivel";

    public AsyncRelayCommand ReloadCertificatesCommand { get; }
    public RelayCommand ClearCertificatesCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand ValidateConfigurationCommand { get; }
    public AsyncRelayCommand StartSyncCommand { get; }
    public AsyncRelayCommand CancelSyncCommand { get; }
    public AsyncRelayCommand ResetNsuCommand { get; }
    public AsyncRelayCommand AnalyzeGapsCommand { get; }
    public AsyncRelayCommand RecoverGapsCommand { get; }
    public AsyncRelayCommand ExportExecutionCommand { get; }
    public AsyncRelayCommand ExportInventoryCommand { get; }
    public AsyncRelayCommand IndexReceivedDocumentsCommand { get; }
    public AsyncRelayCommand ChooseCertificateFolderCommand { get; }
    public AsyncRelayCommand ChooseDestinationFolderCommand { get; }
    public RelayCommand ToggleSimulationModeCommand { get; }

    public string CertificateFolder
    {
        get => _certificateFolder;
        set => SetField(ref _certificateFolder, value);
    }

    public string DestinationFolder
    {
        get => _destinationFolder;
        set => SetField(ref _destinationFolder, value);
    }

    public string SelectedFolderStructure
    {
        get => _selectedFolderStructure;
        set => SetField(ref _selectedFolderStructure, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                RefreshVisibleCertificates();
                OnPropertyChanged(nameof(VisibleCertificateCount));
                OnPropertyChanged(nameof(EmptyStateMessage));
            }
        }
    }

    public int VisibleCertificateCount => VisibleCertificates.Count;
    public string EmptyStateMessage => !string.IsNullOrWhiteSpace(SearchText)
        ? "Nenhum resultado corresponde ao texto de busca."
        : IsSimulationMode
            ? "Nenhuma empresa simulada disponivel. Desative e ative a simulacao para recarregar os dados."
            : "Nenhum certificado carregado. Escolha uma pasta de certificados para iniciar.";

    public string SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (SetField(ref _selectedEnvironment, value) && Enum.TryParse<EnvironmentType>(value, out var environment))
                ApplyEnvironment(environment);
        }
    }

    public string ActiveEnvironmentLabel => $"Ambiente ativo: {EnvironmentDescription}";
    public string EnvironmentDescription => _environment.Current == EnvironmentType.Restrita
        ? "Restrita (homologacao)"
        : "PRODUCAO (API REAL)";

    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        private set
        {
            if (!SetField(ref _isSimulationMode, value))
                return;
            RefreshVisibleCertificates();
            OnPropertyChanged(nameof(HasSelectedCertificates));
            OnPropertyChanged(nameof(VisibleCertificateCount));
            OnPropertyChanged(nameof(EmptyStateMessage));
            OnPropertyChanged(nameof(CanStartSync));
            OnPropertyChanged(nameof(CanStartSyncReason));
            OnPropertyChanged(nameof(SimulationOutputLabel));
            OnPropertyChanged(nameof(SimulationModeButtonText));
            OnPropertyChanged(nameof(StartSyncButtonText));
            OnPropertyChanged(nameof(DocumentCountLabel));
            StartSyncCommand.RaiseCanExecuteChanged();
            SelectAllCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
            AnalyzeGapsCommand.RaiseCanExecuteChanged();
            RecoverGapsCommand.RaiseCanExecuteChanged();
            ResetNsuCommand.RaiseCanExecuteChanged();
        }
    }

    public bool SimulationFailureEnabled
    {
        get => _simulationFailureEnabled;
        set => SetField(ref _simulationFailureEnabled, value);
    }

    public string SimulationOutputLabel =>
        $"Saida de teste: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NEO-e", "Simulacao")}";
    public string SimulationModeButtonText => IsSimulationMode
        ? "Desativar simulacao (TESTE)"
        : "Ativar simulacao (TESTE)";
    public string StartSyncButtonText => IsSimulationMode ? "Executar simulacao" : "Baixar (XML)";
    public string DocumentCountLabel => IsSimulationMode ? "XML SIMULADOS" : "XML GRAVADOS";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                ReloadCertificatesCommand.RaiseCanExecuteChanged();
                ClearCertificatesCommand.RaiseCanExecuteChanged();
                SelectAllCommand.RaiseCanExecuteChanged();
                ClearSelectionCommand.RaiseCanExecuteChanged();
                ValidateConfigurationCommand.RaiseCanExecuteChanged();
                StartSyncCommand.RaiseCanExecuteChanged();
                ToggleSimulationModeCommand.RaiseCanExecuteChanged();
                CancelSyncCommand.RaiseCanExecuteChanged();
                ResetNsuCommand.RaiseCanExecuteChanged();
                IndexReceivedDocumentsCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanStartSync));
                OnPropertyChanged(nameof(IsOperationRunning));
            }
        }
    }

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            if (SetField(ref _isSyncing, value))
            {
                ReloadCertificatesCommand.RaiseCanExecuteChanged();
                ClearCertificatesCommand.RaiseCanExecuteChanged();
                SelectAllCommand.RaiseCanExecuteChanged();
                ClearSelectionCommand.RaiseCanExecuteChanged();
                ValidateConfigurationCommand.RaiseCanExecuteChanged();
                StartSyncCommand.RaiseCanExecuteChanged();
                ToggleSimulationModeCommand.RaiseCanExecuteChanged();
                CancelSyncCommand.RaiseCanExecuteChanged();
                ResetNsuCommand.RaiseCanExecuteChanged();
                IndexReceivedDocumentsCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SyncProgressText));
                OnPropertyChanged(nameof(TotalEmpresas));
                OnPropertyChanged(nameof(TotalXmlGravados));
                OnPropertyChanged(nameof(TotalErros));
                OnPropertyChanged(nameof(CanStartSync));
                OnPropertyChanged(nameof(IsOperationRunning));
            }
        }
    }

    public int TotalEmpresas => _totalEmpresas;
    public int TotalXmlGravados => _totalDocumentos;
    public int TotalErros => _totalErros;
    public int EmpresasProcessadas => _empresasProcessadas;

    public bool HasSelectedCertificates => GetVisibleCertificates().Any(c => c.Selected);

    public string SyncProgressText => _isSyncing
        ? $"Sincronizando: {_empresasProcessadas}/{_totalEmpresas} empresas | {_totalDocumentos} docs | {_totalErros} erros | Atual: {_currentCnpj} NSU {_currentNsu}"
        : string.Empty;

    public Task InitializeAsync()
    {
        if (!_settings.Certificates.AutoDiscover)
            return Task.CompletedTask;
        if (Directory.Exists(CertificateFolder))
            return DiscoverCertificatesAsync();

        StatusMessage = "Pasta de certificados nao encontrada. Escolha uma pasta para carregar certificados.";
        AddLog(StatusMessage, LogLevel.Warning);
        return Task.CompletedTask;
    }

    public void SetCertificateFolder(string path) => CertificateFolder = path;

    public void SetDestinationFolder(string path) => DestinationFolder = path;

    public void SetPassword(CertificateRowViewModel row, string password)
    {
        row.Password = password;

        var key = GetCredentialKey(row);
        _ = _credentialManager.SavePasswordAsync(key, password, CancellationToken.None);

        AddLog($"Senha informada para {row.FileName}. Salva de forma segura (DPAPI).", LogLevel.Info);
    }

    private async Task DiscoverCertificatesAsync()
    {
        _discoveryCancellation?.Cancel();
        _discoveryCancellation?.Dispose();
        _discoveryCancellation = new CancellationTokenSource();
        IsBusy = true;
        AddLog("Lendo certificados...", LogLevel.Info);

        try
        {
            var certificates = await _discoverCertificates.ExecuteAsync(CertificateFolder, _discoveryCancellation.Token);
            foreach (var simulationRow in Certificates.Where(c => c.IsSimulation).ToList())
                Certificates.Remove(simulationRow);
            Certificates.Clear();
            foreach (var certificate in certificates)
            {
                var row = new CertificateRowViewModel(certificate);

                var key = GetCredentialKey(row);
                var savedPassword = await _credentialManager.GetPasswordAsync(key, _discoveryCancellation.Token);
                if (!string.IsNullOrEmpty(savedPassword))
                {
                    row.Password = savedPassword;
                    AddLog($"Senha carregada do armazenamento seguro para {row.FileName}.", LogLevel.Info);
                }

                AddCertificateRow(row);
            }

            await LoadPersistedNsuAsync(_discoveryCancellation.Token);
            if (IsSimulationMode)
                EnsureSimulationCompanies();

            RefreshVisibleCertificates();
            OnPropertyChanged(nameof(VisibleCertificateCount));
            OnPropertyChanged(nameof(EmptyStateMessage));
            AddLog($"{Certificates.Count} certificado(s) encontrado(s).", LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Leitura de certificados cancelada.";
            AddLog("Leitura de certificados cancelada.", LogLevel.Warning);
        }
        catch (Exception exception)
        {
            StatusMessage = "Falha ao carregar certificados.";
            AddLog(exception.Message, LogLevel.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string GetCredentialKey(CertificateRowViewModel row) => $"cert:{row.Thumbprint}";

    private async Task LoadPersistedNsuAsync(CancellationToken ct)
    {
        foreach (var cert in Certificates.Where(c => !c.IsSimulation))
        {
            ct.ThrowIfCancellationRequested();
            var cnpjStr = cert.ExtractCnpj();
            if (!string.IsNullOrWhiteSpace(cnpjStr) && Cnpj.TryParse(cnpjStr, out var cnpj))
            {
                var estado = await _nsuRepository.GetAsync(cnpj, ct);
                if (estado is not null)
                {
                    cert.UltimoNsu = estado.UltimoNsuConfirmado.Value.ToString();
                }
            }
        }
    }

    private void ClearCertificates()
    {
        foreach (var certificate in GetVisibleCertificates().ToList())
            Certificates.Remove(certificate);
        RefreshVisibleCertificates();
        OnPropertyChanged(nameof(VisibleCertificateCount));
        OnPropertyChanged(nameof(EmptyStateMessage));
        OnPropertyChanged(nameof(HasSelectedCertificates));
        OnPropertyChanged(nameof(CanStartSync));
        AddLog("Lista de certificados limpa.", LogLevel.Info);
    }

    private void SelectAllCertificates()
    {
        foreach (var certificate in VisibleCertificates)
            certificate.Selected = true;
        AddLog("Certificados visiveis selecionados.", LogLevel.Info);
        StartSyncCommand.RaiseCanExecuteChanged();
    }

    private void ClearCertificateSelection()
    {
        foreach (var certificate in VisibleCertificates)
            certificate.Selected = false;
        AddLog("Selecao dos certificados visiveis removida.", LogLevel.Info);
        StartSyncCommand.RaiseCanExecuteChanged();
    }

    private void ValidateConfiguration()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(CertificateFolder) || !Directory.Exists(CertificateFolder))
            errors.Add("A pasta de certificados nao existe.");
        if (string.IsNullOrWhiteSpace(DestinationFolder))
            errors.Add("A pasta de destino nao foi informada.");
        if (!Enum.TryParse<FolderStructure>(SelectedFolderStructure, out _))
            errors.Add("A estrutura de pastas e invalida.");
        if (Certificates.Count > 0 && !Certificates.Any(c => c.Selected))
            errors.Add("Nenhum certificado foi selecionado.");

        if (errors.Count == 0)
        {
            AddLog("Configuracao valida para preparar o lote.", LogLevel.Success);
        }
        else
        {
            AddLog(string.Join("; ", errors), LogLevel.Error);
        }
    }

    private bool CanResetNsu() => !IsSimulationMode && GetVisibleCertificates().Any(c => c.Selected);

    private bool CanAnalyzeGaps() => GetVisibleCertificates().Any(c => c.Selected);

    private bool CanRecoverGaps() => GetVisibleCertificates().Any(c => c.Selected);

    public bool CanStartSync
    {
        get
        {
            if (IsBusy || IsSyncing)
                return false;
            var selected = GetVisibleCertificates().Where(c => c.Selected).ToList();
            return selected.Count > 0 &&
                   (IsSimulationMode ||
                    selected.All(c =>
                        c.HasPassword &&
                        c.HasPrivateKey &&
                        c.IsValidCertificate() &&
                        Cnpj.TryParse(c.ExtractCnpj(), out _)));
        }
    }

    public string CanStartSyncReason
    {
        get
        {
            var selected = GetVisibleCertificates().Where(c => c.Selected).ToList();
            if (selected.Count == 0)
                return "Selecione ao menos uma empresa para iniciar.";
            if (IsSimulationMode)
                return "Execucao local com dados sinteticos; nenhuma chamada de rede sera realizada.";
            var missingPassword = selected.Any(c => !c.HasPassword);
            if (missingPassword)
                return "Informe a senha dos certificados selecionados.";
            if (selected.Any(c => !c.HasPrivateKey || !c.IsValidCertificate()))
                return "Um ou mais certificados nao possuem chave privada valida ou estao expirados.";
            if (selected.Any(c => !Cnpj.TryParse(c.ExtractCnpj(), out _)))
                return "Nao foi possivel identificar um CNPJ valido em um ou mais certificados.";
            if (string.IsNullOrWhiteSpace(CertificateFolder) || !Directory.Exists(CertificateFolder))
                return "Escolha uma pasta valida e carregue os certificados.";
            return string.Empty;
        }
    }

    private async Task StartSyncAsync()
    {
        if (!ValidateBeforeSync())
            return;

        _syncCancellation?.Cancel();
        _syncCancellation?.Dispose();
        _syncCancellation = new CancellationTokenSource();

        IsSyncing = true;
        var selectedCertificates = GetVisibleCertificates().Where(c => c.Selected).ToList();
        _totalEmpresas = selectedCertificates.Count;
        _empresasProcessadas = 0;
        _totalDocumentos = 0;
        _totalErros = 0;
        _lastExecutionRecords = [];
        _syncStartedAt = DateTimeOffset.Now;
        _currentCnpj = string.Empty;
        _currentNsu = string.Empty;
        LogMessages.Clear();
        OnPropertyChanged(nameof(SyncProgressText));
        OnPropertyChanged(nameof(TotalEmpresas));
        OnPropertyChanged(nameof(TotalXmlGravados));
        OnPropertyChanged(nameof(TotalErros));

        StatusMessage = IsSimulationMode
            ? "Simulacao em andamento."
            : "Sincronizacao em andamento.";
        AddLog($"Iniciando sincronizacao de {_totalEmpresas} empresa(s)...", LogLevel.Info);

        try
        {
            if (IsSimulationMode)
            {
                var companies = selectedCertificates
                    .Select(c => new SimulationCompany(c.Subject, Cnpj.Parse(c.ExtractCnpj())))
                    .ToList();
                var simulationResult = await _simularCarteira.Value.ExecuteAsync(
                    companies,
                    SimulationFailureEnabled,
                    _syncCancellation.Token);
                _lastExecutionRecords = simulationResult.Companies
                    .Select(result => new ExecutionRecord(
                        result.Company.Cnpj,
                        result.Company.Name,
                        result.InitialNsu,
                        result.FinalNsu,
                        result.DocumentsProcessed,
                        result.Errors,
                        result.FinishedAt - result.StartedAt,
                        result.StartedAt,
                        result.FinishedAt,
                        result.Errors == 0 ? "Simulacao concluida" : "Simulacao com erros"))
                    .ToList();
                StatusMessage = $"Simulacao concluida: {_totalDocumentos} XML simulado(s), {_totalErros} erro(s). Saida isolada em {simulationResult.OutputFolder}.";
                AddLog(
                    $"[TESTE] Lote local concluido: {_totalDocumentos} XML simulado(s), {_totalErros} erro(s).",
                    _totalErros == 0 ? LogLevel.Success : LogLevel.Error);
                NotifyIsland(
                    _totalErros == 0
                        ? $"Simulacao concluida: {_totalDocumentos} XML simulado(s)."
                        : $"Simulacao concluida com {_totalErros} erro(s).",
                    _totalErros == 0 ? IslandNotificationKind.Success : IslandNotificationKind.Error);
                return;
            }

            var empresas = BuildEmpresasFromCertificates();
            var result = await _sincronizarCarteira.Value.ExecuteAsync(
                empresas,
                GetCertificateAsync,
                false,
                _syncCancellation.Token);
            var completedAt = DateTimeOffset.Now;
            _empresasProcessadas = result.Resultados.Count;
            _totalDocumentos = result.TotalDocumentos;
            _totalErros = result.TotalErros;
            OnPropertyChanged(nameof(EmpresasProcessadas));
            OnPropertyChanged(nameof(TotalEmpresas));
            OnPropertyChanged(nameof(TotalXmlGravados));
            OnPropertyChanged(nameof(TotalErros));
            OnPropertyChanged(nameof(SyncProgressText));
            _lastExecutionRecords = result.Resultados
                .Select(item =>
                {
                    var certificate = selectedCertificates.FirstOrDefault(c =>
                        Cnpj.TryParse(c.ExtractCnpj(), out var cnpj) && cnpj == item.Cnpj);
                    var initialNsu = certificate is not null &&
                                     long.TryParse(certificate.UltimoNsu, out var parsedNsu)
                        ? new Nsu(parsedNsu)
                        : Nsu.Zero;
                    return new ExecutionRecord(
                        item.Cnpj,
                        certificate?.Subject ?? item.Cnpj.Format(),
                        initialNsu,
                        item.UltimoNsu,
                        item.DocumentosProcessados,
                        item.Erros,
                        completedAt - _syncStartedAt,
                        _syncStartedAt,
                        completedAt,
                        item.Sucesso ? "Sucesso" : item.Erro ?? "Falha");
                })
                .ToList();

            if (result.Falhas == 0)
            {
                StatusMessage = $"Sincronizacao concluida: {result.TotalDocumentos} documento(s), sem erros.";
                AddLog($"Sincronizacao concluida: {result.Sucessos} sucesso(s), {result.TotalDocumentos} documento(s) processados.", LogLevel.Success);
                NotifyIsland(StatusMessage, IslandNotificationKind.Success);
            }
            else
            {
                StatusMessage = $"Sincronizacao concluida com {result.Falhas} falha(s) e {result.TotalErros} erro(s).";
                AddLog($"Sincronizacao concluida com falhas: {result.Falhas} falha(s), {result.TotalErros} erro(s).", LogLevel.Error);
                NotifyIsland(StatusMessage, IslandNotificationKind.Error);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Operacao cancelada.";
            AddLog("Sincronizacao cancelada pelo usuario.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusMessage = "Falha durante a operacao.";
            AddLog($"Erro durante sincronizacao: {ex.Message}", LogLevel.Error);
            NotifyIsland(StatusMessage, IslandNotificationKind.Error);
        }
        finally
        {
            IsSyncing = false;
            _syncCancellation?.Dispose();
            _syncCancellation = null;
            OnPropertyChanged(nameof(TotalEmpresas));
            OnPropertyChanged(nameof(TotalXmlGravados));
            OnPropertyChanged(nameof(TotalErros));
        }
    }

    private bool ValidateBeforeSync()
    {
        if (IsSimulationMode)
        {
            if (!GetVisibleCertificates().Any(c => c.Selected))
            {
                AddLog("Selecione ao menos uma empresa para a simulacao.", LogLevel.Error);
                return false;
            }
            return true;
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(CertificateFolder) || !Directory.Exists(CertificateFolder))
            errors.Add("A pasta de certificados nao existe.");
        if (string.IsNullOrWhiteSpace(DestinationFolder))
            errors.Add("A pasta de destino nao foi informada.");
        if (!Enum.TryParse<FolderStructure>(SelectedFolderStructure, out _))
            errors.Add("A estrutura de pastas e invalida.");

        var selectedCerts = GetVisibleCertificates().Where(c => c.Selected).ToList();
        if (selectedCerts.Count == 0)
            errors.Add("Nenhum certificado foi selecionado.");
        else
        {
            foreach (var cert in selectedCerts)
            {
                if (!cert.HasPassword)
                    errors.Add($"Senha nao informada para {cert.FileName}.");
                if (!cert.HasPrivateKey)
                    errors.Add($"Certificado {cert.FileName} nao possui chave privada.");
                if (!cert.IsValidCertificate())
                    errors.Add($"Certificado {cert.FileName} invalido ou expirado.");
                if (!Cnpj.TryParse(cert.ExtractCnpj(), out _))
                    errors.Add($"CNPJ nao identificado ou invalido no certificado {cert.FileName}.");
            }
        }

        if (errors.Count > 0)
        {
            AddLog(string.Join("; ", errors), LogLevel.Error);
            return false;
        }

        if (_environment.IsProducao)
        {
            AddLog("Producao selecionada. Confirme o ambiente antes de qualquer operacao fiscal.", LogLevel.Error);
            return false;
        }

        return true;
    }

    private IReadOnlyList<Empresa> BuildEmpresasFromCertificates()
    {
        var empresas = new List<Empresa>();
        foreach (var cert in GetVisibleCertificates().Where(c => c.Selected))
        {
            if (Cnpj.TryParse(cert.ExtractCnpj(), out var cnpj))
            {
                var empresa = Empresa.Create(cnpj, cert.Subject, cert.FilePath);
                empresa.Selecionar();
                empresa.DefinirSenhaInformada(cert.HasPassword);
                if (cert.ValidUntil.HasValue)
                    empresa.AtualizarCertificadoInfo(cert.Thumbprint, cert.ValidUntil.Value, cert.Subject, cert.Issuer);
                empresas.Add(empresa);
            }
        }
        return empresas;
    }

    private async Task<X509Certificate2?> GetCertificateAsync(Empresa empresa)
    {
        var certRow = Certificates.FirstOrDefault(c => c.FilePath == empresa.CertificadoArquivo);
        if (certRow is null || string.IsNullOrEmpty(certRow.Password))
            return null;

        return await _loadCertificate.ExecuteAsync(empresa.CertificadoArquivo, certRow.Password, _syncCancellation!.Token);
    }

    private Task CancelSyncAsync()
    {
        if (IsSyncing)
            _syncCancellation?.Cancel();
        else if (_operationCancellation is not null)
            _operationCancellation.Cancel();
        else
            _discoveryCancellation?.Cancel();
        StatusMessage = "Cancelamento solicitado...";
        AddLog("Cancelamento solicitado. A operacao atual sera interrompida com seguranca.", LogLevel.Warning);
        return Task.CompletedTask;
    }

    private async Task ResetNsuAsync()
    {
        if (!CanResetNsu())
            return;

        var selected = GetVisibleCertificates().Where(c => c.Selected).ToList();

        if (!_resetNsuConfirmed)
        {
            _resetNsuConfirmed = true;
            AddLog($"CONFIRMACAO NECESSARIA: Reset de NSU para {selected.Count} empresa(s). Clique novamente para confirmar.", LogLevel.Warning);
            return;
        }

        _resetNsuConfirmed = false;
        IsBusy = true;
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        var cancellationToken = _operationCancellation.Token;

        var companies = new List<Empresa>();
        foreach (var cert in selected)
        {
            if (Cnpj.TryParse(cert.ExtractCnpj(), out var cnpj))
            {
                var company = Empresa.Create(cnpj, cert.Subject, cert.FilePath);
                company.Selecionar();
                companies.Add(company);
            }
        }

        try
        {
            var result = await _resetNsuUseCase.Value.ExecuteAsync(companies, cancellationToken);
            foreach (var cert in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Cnpj.TryParse(cert.ExtractCnpj(), out var cnpj))
                {
                    var state = await _nsuRepository.GetAsync(cnpj, cancellationToken);
                    cert.UltimoNsu = state?.UltimoNsuConfirmado.Value.ToString() ?? "0";
                }
            }

            StatusMessage = $"Reset de NSU concluido: {result.Sucessos} sucesso(s), {result.Falhas} falha(s).";
            AddLog(StatusMessage, result.Falhas > 0 ? LogLevel.Error : LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Reset de NSU cancelado.";
            AddLog(StatusMessage, LogLevel.Warning);
        }
        catch (Exception exception)
        {
            StatusMessage = "Falha ao resetar o NSU.";
            AddLog($"Erro ao resetar NSU: {exception.Message}", LogLevel.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            IsBusy = false;
        }
    }

    private async Task AnalyzeGapsAsync()
    {
        if (!CanAnalyzeGaps())
            return;

        var selected = GetVisibleCertificates().Where(c => c.Selected).ToList();
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        var cancellationToken = _operationCancellation.Token;
        IsBusy = true;
        AddLog($"Iniciando analise de lacunas para {selected.Count} empresa(s)...", LogLevel.Info);

        try
        {
            if (IsSimulationMode)
            {
                foreach (var certificate in selected)
                    AddLog($"[TESTE] Empresa {certificate.Subject}: 2 lacuna(s) em 1 intervalo (NSU 2 a 3).", LogLevel.Warning);
                StatusMessage = "Analise simulada de lacunas concluida.";
                AddLog("[TESTE] Analise de lacunas concluida sem consultar a API.", LogLevel.Success);
                return;
            }

            foreach (var cert in selected)
            {
                if (Cnpj.TryParse(cert.ExtractCnpj(), out var cnpj))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await _gapAnalyzer.AnalyzeAsync(cnpj, cancellationToken);
                    AddLog($"Empresa {cert.Subject} ({cnpj.Format()}): {result.TotalLacunas} lacuna(s) em {result.Intervalos.Count} intervalo(s)",
                        result.TotalLacunas > 0 ? LogLevel.Warning : LogLevel.Success);

                    foreach (var intervalo in result.Intervalos)
                    {
                        AddLog($"  Lacuna: NSU {intervalo.Inicio} a {intervalo.Fim} ({intervalo.Quantidade} NSUs)", LogLevel.Info);
                    }
                }
            }
            StatusMessage = "Analise de lacunas concluida.";
            AddLog("Analise de lacunas concluida.", LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Analise de lacunas cancelada.";
            AddLog("Analise de lacunas cancelada.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusMessage = "Falha durante a analise de lacunas.";
            AddLog($"Erro durante analise de lacunas: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            IsBusy = false;
        }
    }

    private async Task RecoverGapsAsync()
    {
        if (!CanRecoverGaps())
            return;

        var selected = GetVisibleCertificates().Where(c => c.Selected).ToList();
        if (IsSimulationMode)
        {
            StatusMessage = "Recuperacao simulada concluida: 2 NSUs de teste por empresa.";
            foreach (var certificate in selected)
                AddLog($"[TESTE] Empresa {certificate.Subject}: 2 NSUs simulados recuperados.", LogLevel.Success);
            return;
        }

        IsBusy = true;
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        var cancellationToken = _operationCancellation.Token;
        try
        {
            var totalRecuperados = 0;
            var totalFalhas = 0;

            foreach (var cert in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Cnpj.TryParse(cert.ExtractCnpj(), out var cnpj))
                    continue;

                var analysis = await _gapAnalyzer.AnalyzeAsync(cnpj, cancellationToken);
                var companyNsus = analysis.Intervalos
                    .SelectMany(interval =>
                    {
                        var nsus = new List<Nsu>();
                        for (var nsu = interval.Inicio; nsu <= interval.Fim; nsu = new Nsu(nsu.Value + 1))
                            nsus.Add(nsu);
                        return nsus;
                    })
                    .ToList();

                if (companyNsus.Count == 0)
                {
                    AddLog($"Empresa {cert.Subject}: nenhuma lacuna encontrada.", LogLevel.Info);
                    continue;
                }

                var result = await _gapAnalyzer.RecoverAsync(cnpj, companyNsus, cancellationToken);
                totalRecuperados += result.Recuperados;
                totalFalhas += result.Falhas;
                AddLog(
                    $"Empresa {cert.Subject} ({cnpj.Format()}): {result.Recuperados} recuperado(s), {result.Falhas} falha(s)",
                    result.Falhas > 0 ? LogLevel.Error : LogLevel.Success);

                foreach (var nsuErro in result.NsuComErro)
                    AddLog($"Falha no NSU {nsuErro}", LogLevel.Error);
            }

            StatusMessage = $"Recuperacao concluida: {totalRecuperados} recuperado(s), {totalFalhas} falha(s).";
            AddLog(StatusMessage, totalFalhas > 0 ? LogLevel.Error : LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Recuperacao de lacunas cancelada.";
            AddLog(StatusMessage, LogLevel.Warning);
        }
        catch (Exception exception)
        {
            StatusMessage = "Falha durante a recuperacao de lacunas.";
            AddLog($"Erro durante recuperacao: {exception.Message}", LogLevel.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            IsBusy = false;
        }
    }

    private async Task ExportExecutionAsync()
    {
        if (_lastExecutionRecords.Count == 0)
        {
            StatusMessage = "Ainda nao ha uma execucao para exportar.";
            AddLog(StatusMessage, LogLevel.Warning);
            return;
        }

        AddLog("Exportando relatorio de execucao...", LogLevel.Info);

        try
        {
            var targetPath = await _pickers.PickSaveFileAsync(
                $"Execucao_NEO-e_{DateTimeOffset.Now:yyyyMMdd_HHmmss}",
                ("Excel", ".xlsx"),
                ("CSV", ".csv"));

            if (targetPath is null)
                return;

            var bytes = await _excelExporter.ExportExecutionAsync(_lastExecutionRecords, CancellationToken.None);
            await File.WriteAllBytesAsync(targetPath, bytes);
            StatusMessage = "Relatorio Excel exportado.";
            AddLog($"Relatorio de execucao exportado para: {targetPath}", LogLevel.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = "Falha ao exportar o relatorio Excel.";
            AddLog($"Erro ao exportar relatorio de execucao: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task ExportInventoryAsync()
    {
        AddLog("Exportando inventario de documentos...", LogLevel.Info);

        try
        {
            var targetPath = await _pickers.PickSaveFileAsync(
                $"Inventario_NEO-e_{DateTimeOffset.Now:yyyyMMdd_HHmmss}",
                ("Excel", ".xlsx"),
                ("CSV", ".csv"));

            if (targetPath is null)
                return;

            var allRecords = new List<InventoryRecord>();

            foreach (var cert in Certificates.Where(c => c.Selected))
            {
                if (Cnpj.TryParse(cert.ExtractCnpj(), out _))
                {
                    _ = await _nsuRepository.GetAllAsync(CancellationToken.None);
                }
            }

            var bytes = await _excelExporter.ExportInventoryAsync(allRecords, CancellationToken.None);
            await File.WriteAllBytesAsync(targetPath, bytes);
            AddLog($"Inventario exportado para: {targetPath}", LogLevel.Success);
        }
        catch (Exception ex)
        {
            AddLog($"Erro ao exportar inventario: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task IndexReceivedDocumentsAsync()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        var cancellationToken = _operationCancellation.Token;
        IsBusy = true;
        StatusMessage = "Indexando documentos fiscais existentes...";

        try
        {
            var result = await Task.Run(
                () => _receivedDocumentIndexer.IndexAsync(DestinationFolder, cancellationToken),
                cancellationToken);
            AddLog(
                $"Indexacao concluida: {result.Indexed} novo(s), {result.Skipped} ignorado(s), {result.Errors.Count} erro(s).",
                result.Errors.Count > 0 ? LogLevel.Warning : LogLevel.Success);

            foreach (var error in result.Errors)
                AddLog($"Falha ao indexar {Path.GetFileName(error.FilePath)}: {error.Message}", LogLevel.Error);

            StatusMessage = result.Errors.Count == 0
                ? "Indexacao concluida."
                : $"Indexacao concluida com {result.Errors.Count} erro(s).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Indexacao cancelada.";
            AddLog(StatusMessage, LogLevel.Warning);
        }
        catch (Exception ex)
        {
            StatusMessage = "Falha ao indexar documentos existentes.";
            AddLog($"Falha na indexacao: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            IsBusy = false;
        }
    }

    private async Task ChooseDestinationFolderAsync()
    {
        var folder = await _pickers.PickFolderAsync();
        if (folder is not null)
            SetDestinationFolder(folder);
    }

    private async Task ChooseCertificateFolderAsync()
    {
        var folder = await _pickers.PickFolderAsync();
        if (folder is not null)
        {
            SetCertificateFolder(folder);
            await DiscoverCertificatesAsync();
        }
    }

    private void RefreshVisibleCertificates()
    {
        VisibleCertificates.Clear();
        foreach (var certificate in Certificates)
        {
            if (MatchesFilter(certificate))
                VisibleCertificates.Add(certificate);
        }
    }

    private bool MatchesFilter(CertificateRowViewModel certificate)
    {
        if (certificate.IsSimulation != IsSimulationMode)
            return false;
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;
        return certificate.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               certificate.FilePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               certificate.Subject.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               certificate.Cnpj.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               certificate.Thumbprint.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<CertificateRowViewModel> GetVisibleCertificates() =>
        Certificates.Where(c => c.IsSimulation == IsSimulationMode);

    private void ToggleSimulationMode()
    {
        if (!IsSimulationMode)
            EnsureSimulationCompanies();

        IsSimulationMode = !IsSimulationMode;
        SimulationFailureEnabled = false;
        StatusMessage = IsSimulationMode
            ? "Simulacao local ativa. Nenhuma API ou certificado sera utilizado."
            : "Modo normal ativo. A sincronizacao exige certificados validos.";
        AddLog(
            IsSimulationMode
                ? "Modo Simulacao (TESTE) ativado. Os dados sao sinteticos e a execucao sera offline."
                : "Modo Simulacao desativado.",
            LogLevel.Info);
    }

    private void EnsureSimulationCompanies()
    {
        if (Certificates.Any(c => c.IsSimulation))
            return;

        AddCertificateRow(CertificateRowViewModel.CreateSimulation(
            "Empresa Simulada Alfa",
            "11222333000181"));
        AddCertificateRow(CertificateRowViewModel.CreateSimulation(
            "Empresa Simulada Beta",
            "11222333000262"));
    }

    private void AddCertificateRow(CertificateRowViewModel row)
    {
        row.PropertyChanged += OnCertificateRowPropertyChanged;
        Certificates.Add(row);
    }

    private void OnCertificateRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CertificateRowViewModel.Selected) or nameof(CertificateRowViewModel.HasPassword))
        {
            OnPropertyChanged(nameof(HasSelectedCertificates));
            OnPropertyChanged(nameof(CanStartSync));
            OnPropertyChanged(nameof(CanStartSyncReason));
            StartSyncCommand.RaiseCanExecuteChanged();
            SelectAllCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
            AnalyzeGapsCommand.RaiseCanExecuteChanged();
            RecoverGapsCommand.RaiseCanExecuteChanged();
            ResetNsuCommand.RaiseCanExecuteChanged();
        }
    }

    private void ApplyEnvironment(EnvironmentType environment)
    {
        try
        {
            _environment.SetEnvironment(environment);
            OnPropertyChanged(nameof(ActiveEnvironmentLabel));
            OnPropertyChanged(nameof(EnvironmentDescription));
            AddLog(environment == EnvironmentType.Producao
                ? "Producao selecionada. Confirme o ambiente antes de qualquer operacao fiscal."
                : "Ambiente de producao restrita selecionado.",
                environment == EnvironmentType.Producao ? LogLevel.Error : LogLevel.Info);
        }
        catch (InvalidOperationException exception)
        {
            _selectedEnvironment = _environment.Current.ToString();
            OnPropertyChanged(nameof(SelectedEnvironment));
            AddLog(exception.Message, LogLevel.Error);
        }
    }

    private void OnEnvironmentChanged(EnvironmentType environment)
    {
        _selectedEnvironment = environment.ToString();
        OnPropertyChanged(nameof(SelectedEnvironment));
        OnPropertyChanged(nameof(ActiveEnvironmentLabel));
        OnPropertyChanged(nameof(EnvironmentDescription));
        NotifyIsland($"Ambiente alterado: {EnvironmentDescription}", IslandNotificationKind.Info);
    }

    private void NotifyIsland(string message, IslandNotificationKind kind) =>
        _islandNotifier.Notify(new IslandNotification(message, kind));

    public void ReportEmpresaStart(Cnpj cnpj, Nsu nsuInicial) =>
        Run(() =>
        {
            _currentCnpj = cnpj.Format();
            _currentNsu = nsuInicial.ToString();
            OnPropertyChanged(nameof(SyncProgressText));
            AddLog($"Processando {cnpj.Format()} (NSU inicial: {nsuInicial})", LogLevel.Info);
        });

    public void ReportDocumentoProcessado(Cnpj cnpj, Nsu nsu, ChaveAcesso chave, TipoDocumento tipo) =>
        Run(() =>
        {
            _totalDocumentos++;
            _currentNsu = nsu.ToString();
            OnPropertyChanged(nameof(SyncProgressText));
            OnPropertyChanged(nameof(TotalXmlGravados));
        });

    public void ReportEmpresaComplete(Cnpj cnpj, int documentosProcessados, int erros) =>
        Run(() =>
        {
            _empresasProcessadas++;
            _totalErros += erros;
            OnPropertyChanged(nameof(TotalErros));
            OnPropertyChanged(nameof(EmpresasProcessadas));
            OnPropertyChanged(nameof(SyncProgressText));
            AddLog($"Concluido {cnpj.Format()}: {documentosProcessados} docs, {erros} erros", erros > 0 ? LogLevel.Error : LogLevel.Success);
        });

    public void ReportEmpresaError(Cnpj cnpj, Exception erro) =>
        Run(() =>
        {
            _empresasProcessadas++;
            _totalErros++;
            OnPropertyChanged(nameof(TotalErros));
            OnPropertyChanged(nameof(EmpresasProcessadas));
            OnPropertyChanged(nameof(SyncProgressText));
            AddLog($"Erro em {cnpj.Format()}: {erro.Message}", LogLevel.Error);
        });

    public void ReportProgress(string mensagem) =>
        Run(() =>
        {
            StatusMessage = mensagem;
            AddLog(mensagem, LogLevel.Info);
        });

    public void ReportWarning(string mensagem) =>
        Run(() => AddLog($"Aviso: {mensagem}", LogLevel.Warning));

    private void Run(Action action)
    {
        if (_dispatcher.HasThreadAccess)
            action();
        else
            _dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    App.DiagLog("run-lambda hr=0x" + ex.HResult.ToString("X8") + " " + ex);
                }
            });
    }

    private void AddLog(string message, LogLevel level) =>
        Run(() =>
        {
            LogMessages.Add(new LogMessage(message, level));
            while (LogMessages.Count > 100)
                LogMessages.RemoveAt(0);
        });

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}