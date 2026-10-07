using System.IO;
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
        if (!Directory.Exists(folderPath))
            return [];

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
                // Tentar ler sem senha primeiro (alguns PFX não têm senha ou permitem leitura parcial)
                var cert = LoadCertificateWithoutPassword(file);
                
                var hasPrivateKey = cert.HasPrivateKey;
                var requiresPassword = !hasPrivateKey && FileHasPrivateKey(file);

                results.Add(new CertificateInfo(
                    FilePath: file,
                    FileName: Path.GetFileName(file),
                    Subject: cert.Subject,
                    Issuer: cert.Issuer,
                    NotBefore: cert.NotBefore,
                    NotAfter: cert.NotAfter,
                    Thumbprint: cert.Thumbprint,
                    HasPrivateKey: hasPrivateKey,
                    RequiresPassword: requiresPassword));
            }
            catch (Exception ex)
            {
                // Arquivo corrompido ou formato inválido
                results.Add(new CertificateInfo(
                    FilePath: file,
                    FileName: Path.GetFileName(file),
                    Subject: null,
                    Issuer: null,
                    NotBefore: null,
                    NotAfter: null,
                    Thumbprint: null,
                    HasPrivateKey: false,
                    RequiresPassword: false,
                    ErrorMessage: ex.Message));
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
            // Usar UserKeySet (não requer admin) e EphemeralKeySet (não persiste em disco)
            var flags = X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.EphemeralKeySet;
            
            var cert = new X509Certificate2(filePath, password, flags);
            
            if (!cert.HasPrivateKey)
                throw new CertificateException("Certificado não possui chave privada", CertificateErrorCode.MissingPrivateKey);

            return await Task.FromResult(cert);
        }
        catch (CryptographicException ex) when (ex.HResult == unchecked((int)0x8009000B)) // NTE_BAD_KEYSET / bad password
        {
            throw new CertificateException("Senha incorreta para o certificado", CertificateErrorCode.InvalidPassword);
        }
        catch (CryptographicException)
        {
            throw new CertificateException("Erro criptográfico ao carregar certificado", CertificateErrorCode.InvalidFormat);
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

        // Comparar apenas CNPJ raiz (8 primeiros dígitos) - certificado de matriz pode consultar filiais
        if (extractedCnpj.Value.Root != expectedCnpj.Root)
        {
            return new CertificateValidationResult(false, CertificateValidationError.CnpjMismatch, extractedCnpj.Value);
        }

        return new CertificateValidationResult(true, CertificateValidationError.None, extractedCnpj.Value);
    }

    private static X509Certificate2 LoadCertificateWithoutPassword(string filePath)
    {
        // Tentar carregar sem senha - alguns PFX permitem leitura de metadados sem senha
        return new X509Certificate2(filePath);
    }

    private static bool FileHasPrivateKey(string filePath)
    {
        try
        {
            // Verificar se o arquivo tem chave privada sem tentar carregar (heurística)
            var bytes = File.ReadAllBytes(filePath);
            // PFX com chave privada geralmente tem tamanho > 2KB
            return bytes.Length > 2048;
        }
        catch
        {
            return false;
        }
    }

    private static Cnpj? ExtractCnpjFromCertificate(X509Certificate2 cert)
    {
        Cnpj result;

        // 1. Tentar OID 2.16.76.1.3.3 (e-CNPJ ICP-Brasil)
        var cnpjOid = "2.16.76.1.3.3";
        var ext = cert.Extensions[cnpjOid];
        if (ext != null)
        {
            try
            {
                var asnData = new System.Security.Cryptography.AsnEncodedData(ext.Oid, ext.RawData);
                var cnpjStr = asnData.Format(true).Replace("\n", "").Replace("\r", "").Replace(" ", "");
                if (Cnpj.TryParse(cnpjStr, out result))
                    return result;
            }
            catch { }
        }

        // 2. Tentar Subject CNPJ=
        var subject = cert.Subject;
        var cnpjMatch = System.Text.RegularExpressions.Regex.Match(subject, @"CNPJ=(\d{14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (cnpjMatch.Success && Cnpj.TryParse(cnpjMatch.Groups[1].Value, out result))
            return result;

        // 3. Tentar SERIALNUMBER= (formato antigo)
        var serialMatch = System.Text.RegularExpressions.Regex.Match(subject, @"SERIALNUMBER=(\d{14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (serialMatch.Success && Cnpj.TryParse(serialMatch.Groups[1].Value, out result))
            return result;

        // 4. Tentar extrair do Subject com formato CN=... CNPJ=...
        var cnMatch = System.Text.RegularExpressions.Regex.Match(subject, @"CN=[^,]+?(\d{14})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (cnMatch.Success && Cnpj.TryParse(cnMatch.Groups[1].Value, out result))
            return result;

        return null;
    }
}