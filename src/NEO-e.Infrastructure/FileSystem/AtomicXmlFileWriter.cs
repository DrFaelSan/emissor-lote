using Microsoft.Extensions.Options;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;
using NEO_e.Infrastructure.Configuration;

namespace NEO_e.Infrastructure.FileSystem;

public sealed class AtomicXmlFileWriter : IFileWriter
{
    private readonly StorageSettings _settings;

    public AtomicXmlFileWriter(IOptions<StorageSettings> settings) => _settings = settings.Value;

    public async Task<string> WriteAsync(DocumentoFiscal documento, CancellationToken ct)
    {
        var directory = BuildDirectory(documento);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{documento.ChaveAcesso.Value}.xml");
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllTextAsync(temporaryPath, documento.XmlContent, new System.Text.UTF8Encoding(false), ct);
            if (File.Exists(path))
            {
                var existing = await File.ReadAllTextAsync(path, ct);
                if (documento.ConteudoIgual(existing))
                    return path;
                throw new IOException($"Arquivo existente possui conteudo diferente: {path}");
            }
            File.Move(temporaryPath, path);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Task<bool> ExistsAsync(DocumentoFiscal documento, CancellationToken ct) => Task.FromResult(File.Exists(Path.Combine(BuildDirectory(documento), $"{documento.ChaveAcesso.Value}.xml")));

    public Task<IReadOnlyList<string>> ListFilesAsync(Cnpj cnpj, TipoDocumento? tipo, DateTimeOffset? inicio, DateTimeOffset? fim, CancellationToken ct)
    {
        var root = Path.Combine(_settings.DestinationPath, cnpj.Value);
        IReadOnlyList<string> files = Directory.Exists(root) ? Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories).ToList() : [];
        return Task.FromResult(files);
    }

    private string BuildDirectory(DocumentoFiscal documento)
    {
        var type = documento.Tipo switch
        {
            TipoDocumento.NfseEmitida or TipoDocumento.NfeEmitida => "emitidas",
            TipoDocumento.Evento => "eventos",
            _ => "recebidas"
        };
        var cnpj = documento.Tipo is TipoDocumento.NfseEmitida or TipoDocumento.NfeEmitida
            ? documento.CnpjEmitente
            : documento.CnpjDestinatario;
        var root = Path.Combine(_settings.DestinationPath, cnpj.Value);
        return _settings.FolderStructure switch
        {
            FolderStructure.Flat => root,
            FolderStructure.YearMonth => Path.Combine(root, documento.DataEmissao.Year.ToString(), documento.DataEmissao.Month.ToString("D2")),
            _ => Path.Combine(root, type, documento.DataEmissao.Year.ToString(), documento.DataEmissao.Month.ToString("D2"))
        };
    }
}