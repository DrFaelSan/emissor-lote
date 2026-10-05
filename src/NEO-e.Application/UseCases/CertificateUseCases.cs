using System.IO;
using System.Security.Cryptography.X509Certificates;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;

namespace NEO_e.Application.UseCases;

public sealed class DiscoverCertificatesUseCase
{
    private readonly ICertificateRepository _certificateRepository;

    public DiscoverCertificatesUseCase(ICertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
    }

    public async Task<IReadOnlyList<CertificateInfo>> ExecuteAsync(string folderPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ConfigurationException("Caminho da pasta de certificados não informado");

        if (!Directory.Exists(folderPath))
            throw new ConfigurationException($"Pasta de certificados não encontrada: {folderPath}");

        return await _certificateRepository.DiscoverCertificatesAsync(folderPath, ct);
    }
}

public sealed class LoadCertificateUseCase
{
    private readonly ICertificateRepository _certificateRepository;

    public LoadCertificateUseCase(ICertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
    }

    public async Task<X509Certificate2?> ExecuteAsync(string filePath, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Caminho do arquivo é obrigatório", nameof(filePath));

        return await _certificateRepository.LoadCertificateAsync(filePath, password, ct);
    }
}

public sealed class ValidateCertificateCnpjUseCase
{
    private readonly ICertificateRepository _certificateRepository;

    public ValidateCertificateCnpjUseCase(ICertificateRepository certificateRepository)
    {
        _certificateRepository = certificateRepository;
    }

    public async Task<CertificateValidationResult> ExecuteAsync(X509Certificate2 certificate, Cnpj expectedCnpj, CancellationToken ct)
    {
        return await _certificateRepository.ValidateCertificateAsync(certificate, expectedCnpj, ct);
    }
}

public sealed class UpdateEmpresaCertificateInfoUseCase
{
    public void Execute(Empresa empresa, CertificateInfo certInfo)
    {
        if (certInfo.Thumbprint is null)
            throw new CertificateException("Certificado sem thumbprint", CertificateErrorCode.InvalidFormat, empresa.Cnpj.ToString());

        empresa.AtualizarCertificadoInfo(
            certInfo.Thumbprint,
            certInfo.NotAfter ?? DateTimeOffset.MaxValue,
            certInfo.Subject ?? string.Empty,
            certInfo.Issuer ?? string.Empty);
    }
}

public sealed class ValidateEmpresaReadyUseCase
{
    public EmpresaValidationResult Execute(Empresa empresa)
    {
        var errors = new List<string>();

        if (!empresa.Selecionada)
            errors.Add("Empresa não selecionada");

        if (!empresa.SenhaInformada)
            errors.Add("Senha do certificado não informada");

        if (!empresa.CertificadoValido)
        {
            if (empresa.CertificadoValidade.HasValue && empresa.CertificadoValidade.Value <= DateTimeOffset.UtcNow)
                errors.Add("Certificado expirado");
            else
                errors.Add("Certificado inválido ou não carregado");
        }

        return new EmpresaValidationResult(errors.Count == 0, errors);
    }
}

public sealed record EmpresaValidationResult(bool IsValid, IReadOnlyList<string> Errors);