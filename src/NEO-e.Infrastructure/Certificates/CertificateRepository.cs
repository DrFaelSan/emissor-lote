using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NEO_e.Application.Contracts;
using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;

namespace NEO_e.Infrastructure.Certificates;

public sealed class CertificateRepository : ICertificateRepository
{
    public async Task<IReadOnlyList<CertificateInfo>> DiscoverCertificatesAsync(string folderPath, CancellationToken ct)
    {
        var files = Directory.GetFiles(folderPath, "*.pfx")
            .Concat(Directory.GetFiles(folderPath, "*.p12"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<CertificateInfo>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var cert = new X509Certificate2(file);
                results.Add(new CertificateInfo(
                    FilePath: file,
                    FileName: Path.GetFileName(file),
                    Subject: cert.Subject,
                    Issuer: cert.Issuer,
                    NotBefore: cert.NotBefore,
                    NotAfter: cert.NotAfter,
                    Thumbprint: cert.Thumbprint,
                    HasPrivateKey: cert.HasPrivateKey));
            }
            catch
            {
                results.Add(new CertificateInfo(
                    FilePath: file,
                    FileName: Path.GetFileName(file),
                    Subject: null,
                    Issuer: null,
                    NotBefore: null,
                    NotAfter: null,
                    Thumbprint: null,
                    HasPrivateKey: false));
            }
        }

        return await Task.FromResult(results.AsReadOnly());
    }

    public async Task<X509Certificate2?> LoadCertificateAsync(string filePath, string password, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var cert = new X509Certificate2(
                filePath,
                password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);

            return await Task.FromResult(cert);
        }
        catch (CryptographicException)
        {
            throw new CertificateException("Senha incorreta para o certificado", CertificateErrorCode.InvalidPassword);
        }
        catch (Exception ex) when (ex is not CertificateException)
        {
            throw new CertificateException($"Erro ao carregar certificado: {ex.Message}", CertificateErrorCode.InvalidFormat, innerException: ex);
        }
    }

    public async Task<CertificateValidationResult> ValidateCertificateAsync(X509Certificate2 certificate, Cnpj expectedCnpj, CancellationToken ct)
    {
        if (!certificate.HasPrivateKey)
        {
            return new CertificateValidationResult(false, CertificateValidationError.MissingPrivateKey, null);
        }

        var now = DateTimeOffset.UtcNow;
        if (certificate.NotAfter < now.DateTime)
        {
            return new CertificateValidationResult(false, CertificateValidationError.Expired, null);
        }

        if (certificate.NotBefore > now.DateTime)
        {
            return new CertificateValidationResult(false, CertificateValidationError.NotYetValid, null);
        }

        var extractedCnpj = ExtractCnpjFromCertificate(certificate);
        if (extractedCnpj is null)
        {
            return new CertificateValidationResult(false, CertificateValidationError.CnpjMismatch, null);
        }

        if (extractedCnpj.Value != expectedCnpj.Value)
        {
            return new CertificateValidationResult(false, CertificateValidationError.CnpjMismatch, extractedCnpj);
        }

        return new CertificateValidationResult(true, CertificateValidationError.None, extractedCnpj);
    }

    private static Cnpj? ExtractCnpjFromCertificate(X509Certificate2 cert)
    {
        var subject = cert.Subject;

        var cnpjMatch = System.Text.RegularExpressions.Regex.Match(subject, @"CNPJ=(\d{14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (cnpjMatch.Success && Cnpj.TryParse(cnpjMatch.Groups[1].Value, out var cnpj))
            return cnpj;

        var serialMatch = System.Text.RegularExpressions.Regex.Match(subject, @"SERIALNUMBER=(\d{14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (serialMatch.Success && Cnpj.TryParse(serialMatch.Groups[1].Value, out var cnpj2))
            return cnpj2;

        return null;
    }
}