using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Infrastructure.Parsers;

public sealed class NfeXmlParser : IXmlParser
{
    private static readonly XNamespace NfeNs = "http://www.portalfiscal.inf.br/nfe";

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
        if (root.Name.Namespace != NfeNs)
            throw new FormatException("Namespace NF-e invalido");

        var nfeElement = root.Name.LocalName switch
        {
            "NFe" => root,
            "nfeProc" => root.Element(NfeNs + "NFe"),
            _ => root.Descendants(NfeNs + "NFe").FirstOrDefault()
        };

        if (nfeElement == null)
            throw new FormatException("Elemento NFe não encontrado");

        var infNFe = nfeElement.Element(NfeNs + "infNFe");
        if (infNFe == null)
            throw new FormatException("Elemento infNFe não encontrado");

        var chave = infNFe.Attribute("Id")?.Value;
        if (string.IsNullOrWhiteSpace(chave))
            throw new FormatException("Chave de acesso (Id) ausente no infNFe");

        if (chave.StartsWith("NFe", StringComparison.OrdinalIgnoreCase))
            chave = chave[3..];

        var protNFe = root.Elements(NfeNs + "protNFe").FirstOrDefault();
        var infProt = protNFe?.Element(NfeNs + "infProt");
        var emit = infNFe.Element(NfeNs + "emit");
        var dest = infNFe.Element(NfeNs + "dest");
        var ide = infNFe.Element(NfeNs + "ide");
        var total = infNFe.Element(NfeNs + "total");
        var icmsTot = total?.Element(NfeNs + "ICMSTot");
        var chaveAcesso = ChaveAcesso.Parse(chave);
        if (chaveAcesso.Value.Length != 44)
            throw new FormatException("Chave de acesso de NF-e deve conter 44 digitos");

        var cnpjEmitente = ReadCnpj(emit, "CNPJ");
        if (chaveAcesso.CnpjEmitente != cnpjEmitente)
            throw new FormatException("CNPJ do emitente nao corresponde a chave de acesso");

        return new DocumentoMetadata(
            chaveAcesso,
            TipoDocumento.NfeRecebida,
            cnpjEmitente,
            ReadCnpj(dest, "CNPJ"),
            ReadRequiredValue(ide, "nNF"),
            ReadRequiredValue(ide, "serie"),
            ReadDate(ide, "dhEmi", "dEmi"),
            ReadOptionalDate(infProt, "dhRecbto"),
            new ValorMonetario(ReadDecimal(icmsTot, "vNF")));
    }

    private static Cnpj ReadCnpj(XElement? parent, string elementName)
    {
        var element = parent?.Element(NfeNs + elementName);
        if (element == null)
            throw new FormatException($"Elemento {elementName} não encontrado");

        var value = element.Value.Trim();
        if (Cnpj.TryParse(value, out var cnpj))
            return cnpj;

        throw new FormatException($"CNPJ inválido: {value}");
    }

    private static DateTimeOffset ReadDate(XElement? parent, params string[] elementNames)
    {
        var value = ReadRequiredValue(parent, elementNames);
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
            return result;

        throw new FormatException($"Data inválida: {value}");
    }

    private static DateTimeOffset? ReadOptionalDate(XElement? parent, params string[] elementNames)
    {
        var value = ReadValue(parent, elementNames);
        if (value is null)
            return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
            return result;

        throw new FormatException($"Data invalida: {value}");
    }

    private static decimal ReadDecimal(XElement? parent, string elementName)
    {
        var element = parent?.Element(NfeNs + elementName);
        if (element == null)
            throw new FormatException($"Elemento {elementName} não encontrado");

        var value = element.Value.Trim();
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) || 
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out result))
            return result;

        throw new FormatException($"Valor inválido: {value}");
    }

    private static string ReadRequiredValue(XElement? parent, params string[] elementNames)
    {
        var value = ReadValue(parent, elementNames);
        return value ?? throw new FormatException($"Elemento {string.Join("/", elementNames)} nao encontrado");
    }

    private static string? ReadValue(XElement? parent, params string[] elementNames)
    {
        foreach (var elementName in elementNames)
        {
            var value = parent?.Element(NfeNs + elementName)?.Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}