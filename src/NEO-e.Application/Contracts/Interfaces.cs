using System.Security.Cryptography.X509Certificates;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Application.Contracts;

public interface ILogger
{
    void LogDebug(string message, params object?[] args);
    void LogInformation(string message, params object?[] args);
    void LogWarning(string message, params object?[] args);
    void LogError(string message, params object?[] args);
    void LogError(Exception exception, string message, params object?[] args);
}

public interface ICertificateRepository
{
    Task<IReadOnlyList<CertificateInfo>> DiscoverCertificatesAsync(string folderPath, CancellationToken ct);
    Task<X509Certificate2?> LoadCertificateAsync(string filePath, string password, CancellationToken ct);
    Task<CertificateValidationResult> ValidateCertificateAsync(X509Certificate2 certificate, Cnpj expectedCnpj, CancellationToken ct);
}

public sealed record CertificateInfo(
    string FilePath,
    string FileName,
    string? Subject,
    string? Issuer,
    DateTimeOffset? NotBefore,
    DateTimeOffset? NotAfter,
    string? Thumbprint,
    bool HasPrivateKey);

public sealed record CertificateValidationResult(
    bool IsValid,
    CertificateValidationError? Error,
    Cnpj? ExtractedCnpj);

public enum CertificateValidationError
{
    None,
    Expired,
    NotYetValid,
    MissingPrivateKey,
    CnpjMismatch,
    InvalidFormat
}

public interface INsuRepository
{
    Task<EstadoSincronizacao?> GetAsync(Cnpj cnpj, CancellationToken ct);
    Task SaveAsync(EstadoSincronizacao estado, CancellationToken ct);
    Task DeleteAsync(Cnpj cnpj, CancellationToken ct);
    Task<IReadOnlyList<EstadoSincronizacao>> GetAllAsync(CancellationToken ct);
}

public interface IDocumentRepository
{
    Task<DocumentoFiscal?> GetByChaveAsync(ChaveAcesso chave, CancellationToken ct);
    Task<IReadOnlyList<DocumentoFiscal>> GetByCnpjAsync(Cnpj cnpj, CancellationToken ct);
    Task<IReadOnlyList<DocumentoFiscal>> GetByCnpjAndPeriodoAsync(Cnpj cnpj, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct);
    Task SaveAsync(DocumentoFiscal documento, CancellationToken ct);
    Task SaveBatchAsync(IEnumerable<DocumentoFiscal> documentos, CancellationToken ct);
}

public interface IReceivedDocumentRepository
{
    Task<DocumentoRecebido?> GetByChaveAsync(ChaveAcesso chave, CancellationToken ct);
    Task<IReadOnlyList<DocumentoRecebido>> GetByCnpjAsync(Cnpj cnpj, CancellationToken ct);
    Task<IReadOnlyList<DocumentoRecebido>> GetByCnpjAndPeriodoAsync(Cnpj cnpj, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct);
    Task<IReadOnlyList<DocumentoRecebido>> GetByStatusAsync(Cnpj cnpj, StatusManifestacao status, CancellationToken ct);
    Task SaveAsync(DocumentoRecebido documento, CancellationToken ct);
    Task SaveBatchAsync(IEnumerable<DocumentoRecebido> documentos, CancellationToken ct);
}

public interface IAdnClient
{
    Task<DfeDistributionResponse> GetDfeAsync(Cnpj cnpj, Nsu nsu, X509Certificate2 certificate, CancellationToken ct);
    Task<EventosResponse?> GetEventosAsync(ChaveAcesso chave, X509Certificate2 certificate, CancellationToken ct);
}

public sealed record DfeDistributionResponse(
    Nsu UltNsu,
    long MaxNsu,
    IReadOnlyList<DfeDocument> Lote);

public sealed record DfeDocument(
    Nsu Nsu,
    ChaveAcesso ChaveAcesso,
    TipoDocumento TipoDocumento,
    DateTimeOffset DataHora,
    string XmlBase64Gzip);

public sealed record EventosResponse(
    IReadOnlyList<EventoDocumento> Eventos);

public sealed record EventoDocumento(
    string TipoEvento,
    DateTimeOffset DataHora,
    string XmlEvento);

// Manifestação (RFC-002)
public sealed record ManifestationEvent(
    string TipoEvento,           // 210210, 210200, 210220, 210240
    ChaveAcesso ChaveAcesso,
    Cnpj CnpjDestinatario,
    DateTimeOffset DataHoraEvento,
    int SequenciaEvento,
    string? Justificativa);      // Obrigatório para 210240

public sealed record ManifestationResult
{
    public bool Sucesso { get; init; }
    public string? Protocolo { get; init; }
    public string? CStat { get; init; }
    public string? XMotivo { get; init; }
    public string? ChaveAcesso { get; init; }
    public string? Erro { get; init; }

    public static ManifestationResult Success(string? protocolo, string? cStat, string? xMotivo, string? chaveAcesso)
        => new() { Sucesso = true, Protocolo = protocolo, CStat = cStat, XMotivo = xMotivo, ChaveAcesso = chaveAcesso };

    public static ManifestationResult Failure(string erro)
        => new() { Sucesso = false, Erro = erro };
}

public sealed record EventStatus
{
    public string TipoEvento { get; init; } = string.Empty;
    public DateTimeOffset DataHora { get; init; }
    public string XmlEvento { get; init; } = string.Empty;
}

public interface IManifestationClient
{
    Task<ManifestationResult> SendEventAsync(ManifestationEvent evento, X509Certificate2 certificate, CancellationToken ct);
    Task<EventStatus?> GetEventStatusAsync(ChaveAcesso chave, X509Certificate2 certificate, CancellationToken ct);
}

public interface IXmlParser
{
    DocumentoFiscal ParseNfse(string xml, Nsu nsu);
    IEnumerable<DocumentoFiscal> ParseLote(IReadOnlyList<DfeDocument> lote);
    DocumentoMetadata ExtractMetadata(string xml);
}

public sealed record DocumentoMetadata(
    ChaveAcesso ChaveAcesso,
    TipoDocumento Tipo,
    Cnpj CnpjEmitente,
    Cnpj CnpjDestinatario,
    string Numero,
    string Serie,
    DateTimeOffset DataEmissao,
    DateTimeOffset? DataAutorizacao,
    ValorMonetario ValorTotal);

public interface IFileWriter
{
    Task<string> WriteAsync(DocumentoFiscal documento, CancellationToken ct);
    Task<bool> ExistsAsync(DocumentoFiscal documento, CancellationToken ct);
    Task<IReadOnlyList<string>> ListFilesAsync(Cnpj cnpj, TipoDocumento? tipo, DateTimeOffset? inicio, DateTimeOffset? fim, CancellationToken ct);
}

public interface IProgressReporter
{
    void ReportEmpresaStart(Cnpj cnpj, Nsu nsuInicial);
    void ReportDocumentoProcessado(Cnpj cnpj, Nsu nsu, ChaveAcesso chave, TipoDocumento tipo);
    void ReportEmpresaComplete(Cnpj cnpj, int documentosProcessados, int erros);
    void ReportEmpresaError(Cnpj cnpj, Exception erro);
    void ReportProgress(string mensagem);
    void ReportWarning(string mensagem);
}

public interface IGapAnalyzer
{
    Task<GapAnalysisResult> AnalyzeAsync(Cnpj cnpj, CancellationToken ct);
    Task<GapRecoveryResult> RecoverAsync(Cnpj cnpj, IReadOnlyList<Nsu> nsus, CancellationToken ct);
}

public sealed record GapAnalysisResult(
    Cnpj Cnpj,
    IReadOnlyList<GapInterval> Intervalos,
    int TotalLacunas);

public sealed record GapInterval(
    Nsu Inicio,
    Nsu Fim,
    int Quantidade);

public sealed record GapRecoveryResult(
    int Recuperados,
    int Falhas,
    IReadOnlyList<Nsu> NsuComErro);

public interface IExcelExporter
{
    Task<byte[]> ExportExecutionAsync(IReadOnlyList<ExecutionRecord> records, CancellationToken ct);
    Task<byte[]> ExportInventoryAsync(IReadOnlyList<InventoryRecord> records, CancellationToken ct);
}

public sealed record ExecutionRecord(
    Cnpj Cnpj,
    string Empresa,
    Nsu NsuInicial,
    Nsu NsuFinal,
    int DocumentosProcessados,
    int Erros,
    TimeSpan Duracao,
    DateTimeOffset Inicio,
    DateTimeOffset? Fim,
    string Status);

public sealed record InventoryRecord(
    Cnpj Cnpj,
    ChaveAcesso ChaveAcesso,
    TipoDocumento Tipo,
    string Numero,
    string Serie,
    DateTimeOffset DataEmissao,
    ValorMonetario Valor,
    string CaminhoArquivo,
    Nsu Nsu);