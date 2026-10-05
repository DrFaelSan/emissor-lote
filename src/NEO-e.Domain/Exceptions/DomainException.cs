namespace NEO_e.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class CertificateException : DomainException
{
    public string? Cnpj { get; }
    public CertificateErrorCode ErrorCode { get; }

    public CertificateException(string message, CertificateErrorCode errorCode, string? cnpj = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Cnpj = cnpj;
    }

    public CertificateException(string message, CertificateErrorCode errorCode, Exception innerException, string? cnpj = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Cnpj = cnpj;
    }
}

public enum CertificateErrorCode
{
    NotFound,
    InvalidPassword,
    MissingPrivateKey,
    Expired,
    NotYetValid,
    CnpjMismatch,
    InvalidFormat,
    AccessDenied
}

public sealed class SynchronizationException : DomainException
{
    public string? Cnpj { get; }
    public long? Nsu { get; }
    public SyncErrorCode ErrorCode { get; }

    public SynchronizationException(string message, SyncErrorCode errorCode, string? cnpj = null, long? nsu = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Cnpj = cnpj;
        Nsu = nsu;
    }

    public SynchronizationException(string message, SyncErrorCode errorCode, Exception innerException, string? cnpj = null, long? nsu = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Cnpj = cnpj;
        Nsu = nsu;
    }
}

public enum SyncErrorCode
{
    ApiError,
    NetworkError,
    InvalidResponse,
    DecompressionFailed,
    XmlMalformed,
    WriteFailed,
    StateCorrupted,
    RateLimited,
    Cancelled,
    NoProgress
}

public sealed class ConfigurationException : DomainException
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ManifestationException : DomainException
{
    public string? ChaveAcesso { get; }
    public string? Cnpj { get; }
    public ManifestationErrorCode ErrorCode { get; }

    public ManifestationException(string message, ManifestationErrorCode errorCode, string? chaveAcesso = null, string? cnpj = null)
        : base(message)
    {
        ErrorCode = errorCode;
        ChaveAcesso = chaveAcesso;
        Cnpj = cnpj;
    }

    public ManifestationException(string message, ManifestationErrorCode errorCode, Exception innerException, string? chaveAcesso = null, string? cnpj = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ChaveAcesso = chaveAcesso;
        Cnpj = cnpj;
    }
}

public enum ManifestationErrorCode
{
    InvalidStateTransition,
    EventRejected,
    InvalidEventData,
    DeadlineExpired,
    DuplicateEvent,
    ApiError,
    NetworkError,
    Cancelled
}

public sealed class ValidationException : DomainException
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationException(string message, IEnumerable<ValidationError> errors) : base(message)
    {
        Errors = errors.ToList().AsReadOnly();
    }

    public ValidationException(IEnumerable<ValidationError> errors) : base("Falha de validação")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}

public sealed record ValidationError(string Property, string Code, string Message);