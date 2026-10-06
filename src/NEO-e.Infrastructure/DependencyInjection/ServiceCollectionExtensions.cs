using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using NEO_e.Application.Contracts;
using NEO_e.Infrastructure.Certificates;
using NEO_e.Infrastructure.Http;
using NEO_e.Infrastructure.Logging;
using NEO_e.Infrastructure.Persistence;
using NEO_e.Infrastructure.Configuration;
using NEO_e.Infrastructure.FileSystem;
using NEO_e.Infrastructure.Parsers;

namespace NEO_e.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConfiguration>(configuration);

        services.AddOptions<AppSettings>().BindConfiguration(AppSettings.SectionName);
        services.AddOptions<CertificateSettings>().BindConfiguration(CertificateSettings.SectionName);
        services.AddOptions<StorageSettings>().BindConfiguration(StorageSettings.SectionName);
        services.AddOptions<AdnSettings>().BindConfiguration(AdnSettings.SectionName);
        services.AddOptions<EnvironmentSettings>().BindConfiguration(EnvironmentSettings.SectionName);
        services.AddOptions<LoggingSettings>().BindConfiguration(LoggingSettings.SectionName);

        services.AddSingleton<IAppSettingsProvider, AppSettingsProvider>();
        services.AddSingleton<IEnvironmentContext, EnvironmentContext>();

        // 1. Registra o ILogger do Serilog
        services.AddSerilog(configuration);

        // 2. Mapeia a interface da camada de Application para a implementação de Infrastructure
        services.AddSingleton<NEO_e.Application.Contracts.ILogger, SerilogLogger>();

        services.AddSingleton<ICertificateRepository, CertificateRepository>();
        services.AddSingleton<IXmlParser, NfseXmlParser>();
        services.AddSingleton<IFileWriter, AtomicXmlFileWriter>();

        services.AddSingleton<INsuRepository>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            var dbPath = Path.Combine(settings.Storage.DestinationPath, ".neo-e", "state.db");
            return new SqliteNsuRepository(dbPath);
        });

        services.AddSingleton<IDocumentRepository>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            var dbPath = Path.Combine(settings.Storage.DestinationPath, ".neo-e", "state.db");
            return new SqliteDocumentRepository(dbPath);
        });

        services.AddSingleton<IGapAnalyzer>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            var dbPath = Path.Combine(settings.Storage.DestinationPath, ".neo-e", "state.db");
            return new SqliteGapAnalyzer(dbPath);
        });

        services.AddSingleton<IReceivedDocumentRepository>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            var dbPath = Path.Combine(settings.Storage.DestinationPath, ".neo-e", "state.db");
            return new SqliteReceivedDocumentRepository(dbPath);
        });

        services.AddSingleton<IAdnClient, AdnHttpClient>();

        return services;
    }

    public static IServiceCollection AddSerilog(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<Serilog.ILogger>(sp =>
        {
            var logSettings = sp.GetRequiredService<IOptions<LoggingSettings>>().Value;

            var logFilePath = string.IsNullOrWhiteSpace(logSettings.LogFilePath)
                ? Path.Combine(AppContext.BaseDirectory, "logs", "app.log")
                : logSettings.LogFilePath;

            var logDirectory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            return new Serilog.LoggerConfiguration()
                .MinimumLevel.Is(Enum.TryParse<Serilog.Events.LogEventLevel>(logSettings.MinimumLevel, true, out var level) ? level : Serilog.Events.LogEventLevel.Information)
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: logSettings.RetainedFileCountLimit,
                    fileSizeLimitBytes: logSettings.FileSizeLimitBytes,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        });

        return services;
    }

    public static IServiceCollection AddAppSettingsValidation(this IServiceCollection services)
    {
        services.AddOptions<AppSettings>()
            .BindConfiguration(AppSettings.SectionName)
            .Validate(settings =>
            {
                if (string.IsNullOrWhiteSpace(settings.Certificates.FolderPath))
                    return false;
                if (string.IsNullOrWhiteSpace(settings.Storage.DestinationPath))
                    return false;
                return true;
            }, "Configuração inválida: pastas de certificados e destino são obrigatórias")
            .ValidateOnStart();

        return services;
    }
}

public interface IAppSettingsProvider
{
    AppSettings Settings { get; }
    CertificateSettings Certificates { get; }
    StorageSettings Storage { get; }
    AdnSettings Adn { get; }
    EnvironmentSettings Environment { get; }
    LoggingSettings Logging { get; }
}

public sealed class AppSettingsProvider : IAppSettingsProvider
{
    private readonly IOptionsMonitor<AppSettings> _options;

    public AppSettingsProvider(IOptionsMonitor<AppSettings> options)
    {
        _options = options;
    }

    public AppSettings Settings => _options.CurrentValue;
    public CertificateSettings Certificates => _options.CurrentValue.Certificates;
    public StorageSettings Storage => _options.CurrentValue.Storage;
    public AdnSettings Adn => _options.CurrentValue.Adn;
    public EnvironmentSettings Environment => _options.CurrentValue.Environment;
    public LoggingSettings Logging => _options.CurrentValue.Logging;
}

public interface IEnvironmentContext
{
    EnvironmentType Current { get; }
    string BaseUrl { get; }
    bool IsProducao { get; }
    void SetEnvironment(EnvironmentType environment);
    event Action<EnvironmentType>? EnvironmentChanged;
}

public sealed class EnvironmentContext : IEnvironmentContext
{
    private readonly IAppSettingsProvider _settings;
    private EnvironmentType _current;

    public EnvironmentContext(IAppSettingsProvider settings)
    {
        _settings = settings;
        _current = settings.Environment.Active;
    }

    public EnvironmentType Current => _current;

    public string BaseUrl => _current == EnvironmentType.Producao
        ? _settings.Adn.BaseUrlProducao
        : _settings.Adn.BaseUrlRestrita;

    public bool IsProducao => _current == EnvironmentType.Producao;

    public event Action<EnvironmentType>? EnvironmentChanged;

    public void SetEnvironment(EnvironmentType environment)
    {
        if (_settings.Environment.RequireExplicitConfirmationForProducao &&
            environment == EnvironmentType.Producao &&
            _current != EnvironmentType.Producao)
        {
            throw new InvalidOperationException(
                "Mudança para ambiente de Produção requer confirmação explícita via UI.");
        }

        if (_current != environment)
        {
            _current = environment;
            EnvironmentChanged?.Invoke(environment);
        }
    }
}