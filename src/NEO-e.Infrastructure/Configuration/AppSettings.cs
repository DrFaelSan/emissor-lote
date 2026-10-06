namespace NEO_e.Infrastructure.Configuration;

public sealed class AppSettings
{
    public const string SectionName = "App";

    public CertificateSettings Certificates { get; init; } = new();
    public StorageSettings Storage { get; init; } = new();
    public AdnSettings Adn { get; init; } = new();
    public EnvironmentSettings Environment { get; init; } = new();
    public LoggingSettings Logging { get; init; } = new();
}

public sealed class CertificateSettings
{
    public const string SectionName = "App:Certificates";

    public string FolderPath { get; init; } = string.Empty;
    public bool AutoDiscover { get; init; } = true;
    public string[] SupportedExtensions { get; init; } = [".pfx", ".p12"];
}

public sealed class StorageSettings
{
    public const string SectionName = "App:Storage";

    public string DestinationPath { get; init; } = string.Empty;
    public FolderStructure FolderStructure { get; init; } = FolderStructure.YearMonthType;
    public bool CreateSubfolders { get; init; } = true;
}

public enum FolderStructure
{
    Flat,
    YearMonthType,
    YearMonth,
    TypeYearMonth
}

public sealed class AdnSettings
{
    public const string SectionName = "App:Adn";

    public string BaseUrlRestrita { get; init; } = "https://adn.producaorestrita.nfse.gov.br/";
    public string BaseUrlProducao { get; init; } = "https://adn.nfse.gov.br/";
    public int TimeoutSeconds { get; init; } = 60;
    public int MaxBatchSize { get; init; } = 50;
    public int RateLimitPerMinute { get; init; } = 300;
    public RetrySettings Retry { get; init; } = new();
}

public sealed class RetrySettings
{
    public int MaxAttempts { get; init; } = 3;
    public int BaseDelayMs { get; init; } = 500;
    public int MaxDelayMs { get; init; } = 10000;
    public double BackoffMultiplier { get; init; } = 2.0;
    public bool RespectRetryAfter { get; init; } = true;
    public int[] RetryableStatusCodes { get; init; } = [429, 500, 502, 503, 504];
}

public sealed class EnvironmentSettings
{
    public const string SectionName = "App:Environment";

    public EnvironmentType Active { get; init; } = EnvironmentType.Restrita;
    public bool RequireExplicitConfirmationForProducao { get; init; } = true;
}

public enum EnvironmentType
{
    Restrita,
    Producao
}

public sealed class LoggingSettings
{
    public const string SectionName = "App:Logging";

    public string LogFilePath { get; init; } = "logs/neo-e-.log";
    public string MinimumLevel { get; init; } = "Information";
    public int RetainedFileCountLimit { get; init; } = 31;
    public long FileSizeLimitBytes { get; init; } = 10_485_760;
}

public sealed class SefazSettings
{
    public const string SectionName = "App:Sefaz";
    
    public string RecepcaoEventoUrl { get; init; } = "https://nfe.sefaz.sp.gov.br/nfe/autorizacao/RecepcaoEvento";
    public string DistribuicaoDFeUrl { get; init; } = "https://nfe.sefaz.sp.gov.br/nfe/distribuicao/DFePorNSU";
    public int CodigoOrgao { get; init; } = 35;
    public int TpAmb { get; init; } = 2;
    public int TimeoutSeconds { get; init; } = 60;
}

public sealed class AdnManifestationSettings
{
    public const string SectionName = "App:AdnManifestation";
    
    public int TimeoutSeconds { get; init; } = 60;
}