using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Infrastructure.FileSystem;

public sealed class ReceivedDocumentIndexer : IReceivedDocumentIndexer
{
    private readonly IReceivedDocumentRepository _repository;
    private readonly IXmlParser _xmlParser;

    public ReceivedDocumentIndexer(IReceivedDocumentRepository repository, IXmlParser xmlParser)
    {
        _repository = repository;
        _xmlParser = xmlParser;
    }

    public async Task<ReceivedDocumentIndexResult> IndexAsync(string destinationPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (!Directory.Exists(destinationPath))
            throw new DirectoryNotFoundException($"Pasta de destino nao encontrada: {destinationPath}");

        var scanned = 0;
        var indexed = 0;
        var skipped = 0;
        var errors = new List<ReceivedDocumentIndexError>();

        foreach (var companyDirectory in Directory.EnumerateDirectories(destinationPath).Order(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            if (!Cnpj.TryParse(Path.GetFileName(companyDirectory), out var cnpjConsultado))
                continue;

            foreach (var filePath in Directory.EnumerateFiles(companyDirectory, "*.xml", SearchOption.AllDirectories)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                if (IsEventPath(companyDirectory, filePath))
                {
                    skipped++;
                    continue;
                }

                scanned++;
                try
                {
                    var xml = await File.ReadAllTextAsync(filePath, ct);
                    var metadata = _xmlParser.ExtractMetadata(xml);
                    var tipo = TipoDocumentoClassifier.Classify(
                        metadata.Tipo,
                        metadata.CnpjEmitente,
                        metadata.CnpjDestinatario,
                        cnpjConsultado);

                    if (tipo is not (TipoDocumento.NfseRecebida or TipoDocumento.NfeRecebida))
                    {
                        skipped++;
                        continue;
                    }

                    var tipoRecebido = tipo switch
                    {
                        TipoDocumento.NfseRecebida => TipoDocumentoFiscal.NfseRecebida,
                        TipoDocumento.NfeRecebida => TipoDocumentoFiscal.NfeRecebida,
                        _ => throw new InvalidOperationException($"Tipo recebido nao suportado: {tipo}")
                    };

                    var documento = new DocumentoRecebido(
                        metadata.ChaveAcesso,
                        tipoRecebido,
                        metadata.CnpjEmitente,
                        metadata.CnpjDestinatario,
                        metadata.Numero,
                        metadata.Serie,
                        metadata.DataEmissao,
                        metadata.DataAutorizacao,
                        metadata.ValorTotal,
                        xml)
                    {
                        CaminhoArquivo = filePath
                    };

                    var existing = await _repository.GetByChaveAsync(documento.ChaveAcesso, ct);
                    if (existing is not null)
                    {
                        if (!existing.ConteudoIgual(xml))
                            throw new InvalidDataException("Ja existe um documento com a mesma chave e conteudo diferente.");

                        skipped++;
                        continue;
                    }

                    await _repository.SaveAsync(documento, ct);
                    indexed++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add(new ReceivedDocumentIndexError(filePath, ex.Message));
                }
            }
        }

        return new ReceivedDocumentIndexResult(scanned, indexed, skipped, errors);
    }

    private static bool IsEventPath(string companyDirectory, string filePath)
    {
        var relativePath = Path.GetRelativePath(companyDirectory, filePath);
        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => string.Equals(segment, "eventos", StringComparison.OrdinalIgnoreCase));
    }
}
