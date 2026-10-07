using FluentAssertions;
using NEO_e.Infrastructure.Parsers;

namespace NEO_e.UnitTests;

public sealed class NfseXmlParserTests
{
    private const string PrestadorCnpj = "11222333000181";
    private const string TomadorCnpj = "11222333000181";

    [Fact]
    public void ExtractMetadata_reads_chave_from_id_attribute_when_element_is_absent()
    {
        var expectedChave = new string('1', 44);
        var xml = $"""
            <NFSe xmlns="urn:neo-e:test">
              <InfNFS-e Id="NFS{expectedChave}">
                <CNPJPrestador>{PrestadorCnpj}</CNPJPrestador>
                <CNPJTomador>{TomadorCnpj}</CNPJTomador>
                <nNFSe>123</nNFSe>
                <serie>1</serie>
                <dhEmi>2026-01-15T10:00:00-03:00</dhEmi>
                <vLiq>100.50</vLiq>
              </InfNFS-e>
            </NFSe>
            """;

        var metadata = new NfseXmlParser().ExtractMetadata(xml);

        metadata.ChaveAcesso.Value.Should().Be(expectedChave);
        metadata.Numero.Should().Be("123");
        metadata.ValorTotal.Value.Should().Be(100.50m);
    }

    [Fact]
    public void ExtractMetadata_prefers_chave_element_over_id_attribute()
    {
        var expectedChave = new string('2', 44);
        var xml = $"""
            <NFSe xmlns="urn:neo-e:test" Id="NFS{new string('9', 44)}">
              <chNFSe>{expectedChave}</chNFSe>
              <CNPJPrestador>{PrestadorCnpj}</CNPJPrestador>
              <CNPJTomador>{TomadorCnpj}</CNPJTomador>
              <nNFSe>456</nNFSe>
              <serie>2</serie>
              <dhEmi>2026-01-15T10:00:00-03:00</dhEmi>
              <vLiq>10.00</vLiq>
            </NFSe>
            """;

        var metadata = new NfseXmlParser().ExtractMetadata(xml);

        metadata.ChaveAcesso.Value.Should().Be(expectedChave);
    }

    [Fact]
    public void ExtractMetadata_throws_when_no_chave_is_available()
    {
        var xml = """
            <NFSe xmlns="urn:neo-e:test">
              <CNPJPrestador>11222333000181</CNPJPrestador>
            </NFSe>
            """;

        var action = () => new NfseXmlParser().ExtractMetadata(xml);

        action.Should().Throw<FormatException>().WithMessage("*Chave de acesso ausente*");
    }
}
