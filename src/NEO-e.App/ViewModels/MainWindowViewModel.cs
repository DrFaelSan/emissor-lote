using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Data;
using NEO_e.App.Commands;
using NEO_e.Application.Contracts;
using NEO_e.Application.UseCases;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;
using NEO_e.Infrastructure.Configuration;
using NEO_e.Infrastructure.DependencyInjection;

namespace NEO_e.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IProgressReporter
{
    private readonly DiscoverCertificatesUseCase _discoverCertificates;
    private readonly LoadCertificateUseCase _loadCertificate;
    private readonly SincronizarCarteiraUseCase _sincronizarCarteira;
    private readonly IAppSettingsProvider _settings;
    private readonly IEnvironmentContext _environment;
    private CancellationTokenSource? _discoveryCancellation;
    private CancellationTokenSource? _syncCancellation;
    private string _certificateFolder;
    private string _destinationFolder;
    private string _selectedEnvironment;
    private string _selectedFolderStructure;
    private string _searchText = string.Empty;
    private string _statusMessage = "Pronto para configurar a sincronizacao.";
    private bool _isBusy;
    private bool _isSyncing;
    private int _totalEmpresas;
    private int _empresasProcessadas;
    private int _totalDocumentos;
    private int _totalErros;
    private string _currentCnpj = string.Empty;
    private string _currentNsu = string.Empty;

    public MainWindowViewModel(
        DiscoverCertificatesUseCase discoverCertificates,
        LoadCertificateUseCase loadCertificate,
        SincronizarCarteiraUseCase sincronizarCarteira,
        IAppSettingsProvider settings,
        IEnvironmentContext environment)
    {
        _discoverCertificates = discoverCertificates;
        _loadCertificate = loadCertificate;
        _sincronizarCarteira = sincronizarCarteira;
        _settings = settings;
        _environment = environment;
        _certificateFolder = settings.Certificates.FolderPath;
        _destinationFolder = settings.Storage.DestinationPath;
        _selectedEnvironment = environment.Current.ToString();
        _selectedFolderStructure = settings.Storage.FolderStructure.ToString();

        ReloadCertificatesCommand = new AsyncRelayCommand(DiscoverCertificatesAsync, () => !IsBusy && !IsSyncing);
        ClearCertificatesCommand = new RelayCommand(ClearCertificates, () => !IsBusy && !IsSyncing && Certificates.Count > 0);
        SelectAllCommand = new RelayCommand(SelectAllCertificates, () => !IsBusy && !IsSyncing && Certificates.Count > 0);
        ClearSelectionCommand = new RelayCommand(ClearCertificateSelection, () => !IsBusy && !IsSyncing && Certificates.Count > 0);
        ValidateConfigurationCommand = new RelayCommand(ValidateConfiguration, () => !IsBusy && !IsSyncing);
        StartSyncCommand = new AsyncRelayCommand(StartSyncAsync, () => !IsBusy && !IsSyncing && CanStartSync());
        CancelSyncCommand = new AsyncRelayCommand(CancelSyncAsync, () => IsSyncing);
        ResetNsuCommand = new AsyncRelayCommand(ResetNsuAsync, () => !IsBusy && !IsSyncing && CanResetNsu());
        CertificatesView = CollectionViewSource.GetDefaultView(Certificates);
        CertificatesView.Filter = FilterCertificate;
        _environment.EnvironmentChanged += OnEnvironmentChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CertificateRowViewModel> Certificates { get; } = [];

    public IReadOnlyList<string> Environments { get; } = ["Restrita", "Producao"];

    public IReadOnlyList<string> FolderStructures { get; } = ["Flat", "YearMonthType", "YearMonth", "TypeYearMonth"];

    public AsyncRelayCommand ReloadCertificatesCommand { get; }

    public RelayCommand ClearCertificatesCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand ClearSelectionCommand { get; }

    public RelayCommand ValidateConfigurationCommand { get; }

    public AsyncRelayCommand StartSyncCommand { get; }

    public AsyncRelayCommand CancelSyncCommand { get; }

    public AsyncRelayCommand ResetNsuCommand { get; }

    public ICollectionView CertificatesView { get; }

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
                CertificatesView.Refresh();
        }
    }

    public int VisibleCertificateCount => CertificatesView.Cast<object>().Count();

    public string SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (SetField(ref _selectedEnvironment, value) && Enum.TryParse<EnvironmentType>(value, out var environment))
                ApplyEnvironment(environment);
        }
    }

    public string ActiveEnvironmentLabel => $"Ambiente ativo: {_environment.Current}";

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
                ResetNsuCommand.RaiseCanExecuteChanged();
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
                CancelSyncCommand.RaiseCanExecuteChanged();
                ResetNsuCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SyncProgressText));
            }
        }
    }

    public string SyncProgressText => _isSyncing
        ? $"Sincronizando: {_empresasProcessadas}/{_totalEmpresas} empresas | {_totalDocumentos} docs | {_totalErros} erros | Atual: {_currentCnpj} NSU {_currentNsu}"
        : string.Empty;

    public void SetCertificateFolder(string path) => CertificateFolder = path;

    public void SetDestinationFolder(string path) => DestinationFolder = path;

    public void SetPassword(CertificateRowViewModel row, string password)
    {
        row.Password = password;
        StatusMessage = $"Senha informada para {row.FileName}. Ela permanece somente em memoria durante a sessao.";
    }

    private async Task DiscoverCertificatesAsync()
    {
        _discoveryCancellation?.Cancel();
        _discoveryCancellation?.Dispose();
        _discoveryCancellation = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = "Lendo certificados...";

        try
        {
            var certificates = await _discoverCertificates.ExecuteAsync(CertificateFolder, _discoveryCancellation.Token);
            Certificates.Clear();
            foreach (var certificate in certificates)
                Certificates.Add(new CertificateRowViewModel(certificate));
            CertificatesView.Refresh();
            OnPropertyChanged(nameof(VisibleCertificateCount));
            StatusMessage = $"{Certificates.Count} certificado(s) encontrado(s).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Leitura de certificados cancelada.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearCertificates()
    {
        Certificates.Clear();
        CertificatesView.Refresh();
        OnPropertyChanged(nameof(VisibleCertificateCount));
        StatusMessage = "Lista de certificados limpa.";
    }

    private void SelectAllCertificates()
    {
        foreach (var certificate in CertificatesView.Cast<CertificateRowViewModel>())
            certificate.Selected = true;
        StatusMessage = "Certificados visiveis selecionados.";
        StartSyncCommand.RaiseCanExecuteChanged();
    }

    private void ClearCertificateSelection()
    {
        foreach (var certificate in CertificatesView.Cast<CertificateRowViewModel>())
            certificate.Selected = false;
        StatusMessage = "Selecao dos certificados visiveis removida.";
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

        StatusMessage = errors.Count == 0
            ? "Configuracao valida para preparar o lote."
            : string.Join(" ", errors);
    }

    private bool CanStartSync()
    {
        return Certificates.Any(c => c.Selected && c.HasPassword && c.HasPrivateKey && c.IsValidCertificate());
    }

    private bool CanResetNsu()
    {
        return Certificates.Any(c => c.Selected);
    }

    private async Task StartSyncAsync()
    {
        if (!ValidateBeforeSync())
            return;

        _syncCancellation?.Cancel();
        _syncCancellation?.Dispose();
        _syncCancellation = new CancellationTokenSource();

        IsSyncing = true;
        _totalEmpresas = Certificates.Count(c => c.Selected);
        _empresasProcessadas = 0;
        _totalDocumentos = 0;
        _totalErros = 0;
        _currentCnpj = string.Empty;
        _currentNsu = string.Empty;
        OnPropertyChanged(nameof(SyncProgressText));

        try
        {
            var empresas = BuildEmpresasFromCertificates();
            var result = await _sincronizarCarteira.ExecuteAsync(
                empresas,
                GetCertificateAsync,
                false,
                _syncCancellation.Token);

            StatusMessage = result.Falhas == 0
                ? $"Sincronizacao concluida: {result.Sucessos} sucesso(s), {result.TotalDocumentos} documento(s) processados."
                : $"Sincronizacao concluida com falhas: {result.Falhas} falha(s), {result.TotalErros} erro(s).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sincronizacao cancelada pelo usuario.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro durante sincronizacao: {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
            _syncCancellation?.Dispose();
            _syncCancellation = null;
        }
    }

    private bool ValidateBeforeSync()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(CertificateFolder) || !Directory.Exists(CertificateFolder))
            errors.Add("A pasta de certificados nao existe.");
        if (string.IsNullOrWhiteSpace(DestinationFolder))
            errors.Add("A pasta de destino nao foi informada.");
        if (!Enum.TryParse<FolderStructure>(SelectedFolderStructure, out _))
            errors.Add("A estrutura de pastas e invalida.");

        var selectedCerts = Certificates.Where(c => c.Selected).ToList();
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
            }
        }

        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return false;
        }

        if (_environment.IsProducao)
        {
            StatusMessage = "Producao selecionada. Confirme o ambiente antes de qualquer operacao fiscal.";
            return false;
        }

        return true;
    }

    private IReadOnlyList<Empresa> BuildEmpresasFromCertificates()
    {
        var empresas = new List<Empresa>();
        foreach (var cert in Certificates.Where(c => c.Selected))
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

    private async Task CancelSyncAsync()
    {
        _syncCancellation?.Cancel();
        StatusMessage = "Cancelando sincronizacao...";
    }

    private async Task ResetNsuAsync()
    {
        if (!CanResetNsu())
            return;

        var selected = Certificates.Where(c => c.Selected).ToList();
        StatusMessage = $"Reset de NSU solicitado para {selected.Count} empresa(s). Funcionalidade requer confirmacao em duas etapas.";
    }

    private bool FilterCertificate(object item)
    {
        if (item is not CertificateRowViewModel certificate || string.IsNullOrWhiteSpace(SearchText))
            return true;
        return certificate.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               certificate.Subject.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               certificate.Thumbprint.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyEnvironment(EnvironmentType environment)
    {
        try
        {
            _environment.SetEnvironment(environment);
            OnPropertyChanged(nameof(ActiveEnvironmentLabel));
            StatusMessage = environment == EnvironmentType.Producao
                ? "Producao selecionada. Confirme o ambiente antes de qualquer operacao fiscal."
                : "Ambiente de producao restrita selecionado.";
        }
        catch (InvalidOperationException exception)
        {
            _selectedEnvironment = _environment.Current.ToString();
            OnPropertyChanged(nameof(SelectedEnvironment));
            StatusMessage = exception.Message;
        }
    }

    private void OnEnvironmentChanged(EnvironmentType environment)
    {
        _selectedEnvironment = environment.ToString();
        OnPropertyChanged(nameof(SelectedEnvironment));
        OnPropertyChanged(nameof(ActiveEnvironmentLabel));
    }

    public void ReportEmpresaStart(Cnpj cnpj, Nsu nsuInicial)
    {
        _currentCnpj = cnpj.Format();
        _currentNsu = nsuInicial.ToString();
        _empresasProcessadas++;
        OnPropertyChanged(nameof(SyncProgressText));
        StatusMessage = $"Processando {cnpj.Format()} (NSU inicial: {nsuInicial})";
    }

    public void ReportDocumentoProcessado(Cnpj cnpj, Nsu nsu, ChaveAcesso chave, TipoDocumento tipo)
    {
        _totalDocumentos++;
        _currentNsu = nsu.ToString();
        OnPropertyChanged(nameof(SyncProgressText));
    }

    public void ReportEmpresaComplete(Cnpj cnpj, int documentosProcessados, int erros)
    {
        _totalErros += erros;
        StatusMessage = $"Concluido {cnpj.Format()}: {documentosProcessados} docs, {erros} erros";
    }

    public void ReportEmpresaError(Cnpj cnpj, Exception erro)
    {
        _totalErros++;
        StatusMessage = $"Erro em {cnpj.Format()}: {erro.Message}";
    }

    public void ReportProgress(string mensagem)
    {
        StatusMessage = mensagem;
    }

    public void ReportWarning(string mensagem)
    {
        StatusMessage = $"Aviso: {mensagem}";
    }

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

public sealed class CertificateRowViewModel : INotifyPropertyChanged
{
    private string _password = string.Empty;
    private bool _selected = true;

    public CertificateRowViewModel(CertificateInfo certificate)
    {
        FilePath = certificate.FilePath;
        FileName = certificate.FileName;
        Subject = certificate.Subject ?? "Nao lido";
        Issuer = certificate.Issuer ?? "Nao lido";
        Thumbprint = certificate.Thumbprint ?? "Nao lido";
        ValidUntil = certificate.NotAfter;
        HasPrivateKey = certificate.HasPrivateKey;
        Status = certificate.HasPrivateKey ? "Disponivel" : "Sem chave privada";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath { get; }
    public string FileName { get; }
    public string Subject { get; }
    public string Issuer { get; }
    public string Thumbprint { get; }
    public DateTimeOffset? ValidUntil { get; }
    public bool HasPrivateKey { get; }
    public string Status { get; }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
                return;
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPassword)));
        }
    }

    public bool HasPassword => !string.IsNullOrEmpty(_password);

    public string ExtractCnpj()
    {
        if (string.IsNullOrWhiteSpace(Subject))
            return string.Empty;

        var cnpjMatch = System.Text.RegularExpressions.Regex.Match(Subject, @"CNPJ=(\d{14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (cnpjMatch.Success)
            return cnpjMatch.Groups[1].Value;

        var serialMatch = System.Text.RegularExpressions.Regex.Match(Subject, @"SERIALNUMBER=(\d{14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (serialMatch.Success)
            return serialMatch.Groups[1].Value;

        return string.Empty;
    }

    public bool IsValidCertificate()
    {
        if (!HasPrivateKey)
            return false;

        if (!ValidUntil.HasValue)
            return false;

        return ValidUntil.Value > DateTimeOffset.UtcNow;
    }
}