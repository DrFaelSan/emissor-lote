using System.IO.Compression;
using System.Xml.Linq;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Infrastructure.Parsers;

public sealed class CompositeXmlParser : IXmlParser
{
    private static readonly XNamespace NfeNs = "http://www.portalfiscal.inf.br/nfe";
    private readonly NfseXmlParser _nfseParser;
    private readonly NfeXmlParser _nfeParser;

    public CompositeXmlParser(NfseXmlParser nfseParser, NfeXmlParser nfeParser)
    {
        _nfseParser = nfseParser;
        _nfeParser = nfeParser;
    }

    public DocumentoFiscal ParseNfse(string xml, Nsu nsu)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (IsNfe(doc))
            return _nfeParser.ParseNfse(xml, nsu);

        return _nfseParser.ParseNfse(xml, nsu);
    }

    public IEnumerable<DocumentoFiscal> ParseLote(IReadOnlyList<DfeDocument> lote)
    {
        foreach (var item in lote)
        {
            var xml = DecodeXml(item.XmlBase64Gzip);
            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

            if (IsNfe(doc))
                yield return _nfeParser.ParseNfse(xml, item.Nsu);
            else
                yield return _nfseParser.ParseNfse(xml, item.Nsu);
        }
    }

    public DocumentoMetadata ExtractMetadata(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (IsNfe(doc))
            return _nfeParser.ExtractMetadata(xml);

        return _nfseParser.ExtractMetadata(xml);
    }

    private static bool IsNfe(XDocument document)
    {
        var root = document.Root;
        return root?.Name.Namespace == NfeNs &&
               root.Name.LocalName is "NFe" or "nfeProc" or "protNFe";
    }

    private static string DecodeXml(string encoded)
    {
        using var input = new MemoryStream(Convert.FromBase64String(encoded));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
}