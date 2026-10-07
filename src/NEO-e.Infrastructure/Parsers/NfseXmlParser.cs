using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Infrastructure.Parsers;

public sealed class NfseXmlParser : IXmlParser
{
    public DocumentoFiscal ParseNfse(string xml, Nsu nsu)
    {
        var metadata = ReadMetadata(XDocument.Parse(xml, LoadOptions.PreserveWhitespace));
        return new DocumentoFiscal(metadata.ChaveAcesso, metadata.Tipo, metadata.CnpjEmitente, metadata.CnpjDestinatario, metadata.Numero, metadata.Serie, metadata.DataEmissao, metadata.DataAutorizacao, metadata.ValorTotal, xml, nsu);
    }

    public IEnumerable<DocumentoFiscal> ParseLote(IReadOnlyList<DfeDocument> lote)
    {
        foreach (var item in lote)
            yield return ParseNfse(DecodeXml(item.XmlBase64Gzip), item.Nsu);
    }

    public DocumentoMetadata ExtractMetadata(string xml) => ReadMetadata(XDocument.Parse(xml, LoadOptions.PreserveWhitespace));

    private static string DecodeXml(string encoded)
    {
        using var input = new MemoryStream(Convert.FromBase64String(encoded));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }

    private static DocumentoMetadata ReadMetadata(XDocument document)
    {
        var root = document.Root ?? throw new FormatException("XML sem elemento raiz");
        var chave = ReadValue(root, "chNFSe", "ChaveAcesso", "Id") ?? throw new FormatException("Chave de acesso ausente");
        if (chave.StartsWith("NFS", StringComparison.OrdinalIgnoreCase))
            chave = chave[3..];

        return new DocumentoMetadata(
            ChaveAcesso.Parse(chave),
            TipoDocumento.NfseRecebida,
            ReadCnpj(root, "CNPJPrestador", "CnpjPrestador", "CNPJEmitente", "CnpjEmitente"),
            ReadCnpj(root, "CNPJTomador", "CnpjTomador", "CNPJDestinatario", "CnpjDestinatario"),
            ReadValue(root, "nNFSe", "Numero", "nDPS") ?? string.Empty,
            ReadValue(root, "serie", "Serie") ?? string.Empty,
            ReadDate(root, "dhEmi", "DataEmissao", "dCompet"),
            ReadOptionalDate(root, "dhProc", "DataAutorizacao", "dhAutorizacao"),
            new ValorMonetario(ReadDecimal(root, "vLiq", "ValorLiquido", "vServ", "ValorTotal", "vNF")));
    }

    private static Cnpj ReadCnpj(XElement root, params string[] names)
    {
        foreach (var name in names)
        {
            var value = ReadValue(root, name);
            if (value is not null && Cnpj.TryParse(value, out var cnpj))
                return cnpj;
        }
        throw new FormatException($"CNPJ ausente: {string.Join(", ", names)}");
    }

    private static DateTimeOffset ReadDate(XElement root, params string[] names)
    {
        var value = ReadValue(root, names) ?? throw new FormatException($"Data ausente: {string.Join(", ", names)}");
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
            return result;
        throw new FormatException($"Data invalida: {value}");
    }

    private static DateTimeOffset? ReadOptionalDate(XElement root, params string[] names)
    {
        var value = ReadValue(root, names);
        return value is not null && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ? result : null;
    }

    private static decimal ReadDecimal(XElement root, params string[] names)
    {
        var value = ReadValue(root, names) ?? "0";
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) || decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out result))
            return result;
        throw new FormatException($"Valor invalido: {value}");
    }

    private static string? ReadValue(XElement root, params string[] names)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            if (names.Any(name => string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)))
            {
                var value = element.Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes())
            {
                if (names.Any(name => string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)))
                {
                    var value = attribute.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
        }

        return null;
    }
}