using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;
using NEO_e.Infrastructure.Configuration;
using NEO_e.Infrastructure.DependencyInjection;
using NEO_e.Infrastructure.FileSystem;
using NEO_e.Infrastructure.Parsers;
using NEO_e.Infrastructure.Persistence;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace NEO_e.UnitTests;

public sealed class CoreAndSynchronizationTests
{
    private const string ValidCnpj = "11222333000181";
    private static readonly string ValidKey = "123456" + ValidCnpj + new string('0', 30);

    [Fact]
    public void Nsu_rejects_negative_values()
    {
        var action = () => new Nsu(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Estado_does_not_move_backwards()
    {
        var estado = EstadoSincronizacao.Create(Cnpj.Parse(ValidCnpj));
        estado.ConfirmarNsu(new Nsu(10));
        estado.ConfirmarNsu(new Nsu(5));
        estado.UltimoNsuConfirmado.Value.Should().Be(10);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Infrastructure_registers_application_logger_with_serilog_dependency()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["App:Logging:LogFilePath"] = Path.Combine(root, "logs", "test-.log")
                })
                .Build();
            var services = new ServiceCollection().AddInfrastructure(configuration);
            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<NEO_e.Application.Contracts.ILogger>()
                .Should().BeOfType<NEO_e.Infrastructure.Logging.SerilogLogger>();
            provider.GetRequiredService<Serilog.ILogger>().Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Parser_decodes_gzip_and_reads_namespaced_xml()
    {
        var parser = new NfseXmlParser();
        var document = parser.ParseLote([
            new DfeDocument(new Nsu(7), ChaveAcesso.Parse(ValidKey), TipoDocumento.NfseRecebida, DateTimeOffset.UtcNow, CompressXml(CreateXml()))
        ]).Single();

        document.ChaveAcesso.Value.Should().Be(ValidKey);
        document.CnpjEmitente.Value.Should().Be(ValidCnpj);
        document.ValorTotal.Value.Should().Be(123.45m);
        document.Nsu.Value.Should().Be(7);
    }

    [Fact]
    public void Nfe_parser_reads_namespaced_nfe_and_processed_protocol()
    {
        var emitter = Cnpj.Parse("11222333000262");
        var recipient = Cnpj.Parse(ValidCnpj);
        var xml = CreateNfeXml(emitter, recipient);

        var metadata = new NfeXmlParser().ExtractMetadata(xml);
        var nfeXml = XDocument.Parse(xml)
            .Root!
            .Element(XName.Get("NFe", "http://www.portalfiscal.inf.br/nfe"))!
            .ToString(SaveOptions.DisableFormatting);
        var unprocessedMetadata = new NfeXmlParser().ExtractMetadata(nfeXml);
        var document = new CompositeXmlParser(new NfseXmlParser(), new NfeXmlParser())
            .ParseNfse(xml, new Nsu(12));

        metadata.ChaveAcesso.Value.Should().HaveLength(44);
        metadata.CnpjEmitente.Should().Be(emitter);
        metadata.CnpjDestinatario.Should().Be(recipient);
        metadata.Numero.Should().Be("42");
        metadata.Serie.Should().Be("1");
        metadata.DataAutorizacao.Should().NotBeNull();
        metadata.ValorTotal.Value.Should().Be(123.45m);
        unprocessedMetadata.ChaveAcesso.Should().Be(metadata.ChaveAcesso);
        unprocessedMetadata.DataAutorizacao.Should().BeNull();
        document.Tipo.Should().Be(TipoDocumento.NfeRecebida);
        document.Nsu.Value.Should().Be(12);
    }

    [Fact]
    public void Document_classification_uses_cnpj_root_and_exact_branch()
    {
        var headOffice = Cnpj.Parse(ValidCnpj);
        var branch = Cnpj.Parse("11222333000262");
        var issued = new DocumentoFiscal(
            ChaveAcesso.Parse(CreateNfeKey(headOffice)),
            TipoDocumento.NfeRecebida,
            headOffice,
            branch,
            "42",
            "1",
            DateTimeOffset.Parse("2026-09-23T10:00:00-03:00"),
            null,
            new ValorMonetario(123.45m),
            "<xml />",
            Nsu.Zero);
        var received = new DocumentoFiscal(
            ChaveAcesso.Parse(CreateNfeKey(branch)),
            TipoDocumento.NfeRecebida,
            branch,
            headOffice,
            "43",
            "1",
            DateTimeOffset.Parse("2026-09-23T10:00:00-03:00"),
            null,
            new ValorMonetario(123.45m),
            "<xml />",
            Nsu.Zero);

        issued.ClassificarPara(headOffice);
        received.ClassificarPara(headOffice);

        issued.Tipo.Should().Be(TipoDocumento.NfeEmitida);
        received.Tipo.Should().Be(TipoDocumento.NfeRecebida);
        TipoDocumentoClassifier.Classify(
            TipoDocumento.NfseRecebida,
            headOffice,
            branch,
            headOffice).Should().Be(TipoDocumento.NfseEmitida);
    }

    [Fact]
    public async Task Emitted_document_is_written_under_emitter_cnpj_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-{Guid.NewGuid():N}");
        try
        {
            var emitter = Cnpj.Parse(ValidCnpj);
            var recipient = Cnpj.Parse("11222333000262");
            var document = new NfeXmlParser().ParseNfse(CreateNfeXml(emitter, recipient), Nsu.Zero);
            document.ClassificarPara(emitter);
            var writer = new AtomicXmlFileWriter(Options.Create(new StorageSettings
            {
                DestinationPath = root,
                FolderStructure = FolderStructure.YearMonthType
            }));

            var filePath = await writer.WriteAsync(document, CancellationToken.None);

            document.Tipo.Should().Be(TipoDocumento.NfeEmitida);
            Path.GetRelativePath(root, filePath).Should().StartWith(emitter.Value);
            Path.GetRelativePath(root, filePath).Should().Contain("emitidas");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Received_document_indexer_scans_tree_and_skips_duplicate_content()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-{Guid.NewGuid():N}");
        try
        {
            var company = Cnpj.Parse(ValidCnpj);
            var branch = Cnpj.Parse("11222333000262");
            var receivedDirectory = Path.Combine(root, company.Value, "recebidas", "2026", "09");
            var issuedDirectory = Path.Combine(root, company.Value, "emitidas", "2026", "09");
            Directory.CreateDirectory(receivedDirectory);
            Directory.CreateDirectory(issuedDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(receivedDirectory, $"{CreateNfeKey(branch)}.xml"),
                CreateNfeXml(branch, company));
            await File.WriteAllTextAsync(
                Path.Combine(issuedDirectory, $"{CreateNfeKey(company)}.xml"),
                CreateNfeXml(company, branch));

            var repository = new SqliteReceivedDocumentRepository(Path.Combine(root, ".neo-e", "state.db"));
            var indexer = new ReceivedDocumentIndexer(repository, new CompositeXmlParser(new NfseXmlParser(), new NfeXmlParser()));

            var first = await indexer.IndexAsync(root, CancellationToken.None);
            var second = await indexer.IndexAsync(root, CancellationToken.None);

            first.Scanned.Should().Be(2);
            first.Indexed.Should().Be(1);
            first.Skipped.Should().Be(1);
            first.Errors.Should().BeEmpty();
            second.Indexed.Should().Be(0);
            second.Skipped.Should().Be(2);
            (await repository.GetByCnpjAsync(company, CancellationToken.None))
                .Should().ContainSingle()
                .Which.Tipo.Should().Be(TipoDocumentoFiscal.NfeRecebida);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Writer_is_idempotent_for_identical_content()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-{Guid.NewGuid():N}");
        try
        {
            var writer = new AtomicXmlFileWriter(Options.Create(new StorageSettings
            {
                DestinationPath = root,
                FolderStructure = FolderStructure.YearMonthType
            }));
            var parser = new NfseXmlParser();
            var documento = parser.ParseNfse(CreateXml(), new Nsu(1));

            var first = await writer.WriteAsync(documento, CancellationToken.None);
            var second = await writer.WriteAsync(documento, CancellationToken.None);

            first.Should().Be(second);
            File.Exists(first).Should().BeTrue();
            Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Sqlite_document_repository_round_trips_document()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-{Guid.NewGuid():N}");
        try
        {
            var repository = new SqliteDocumentRepository(Path.Combine(root, "state.db"));
            var documento = new NfseXmlParser().ParseNfse(CreateXml(), new Nsu(2));
            await repository.SaveAsync(documento, CancellationToken.None);

            var loaded = await repository.GetByChaveAsync(documento.ChaveAcesso, CancellationToken.None);
            loaded.Should().NotBeNull();
            loaded!.ValorTotal.Value.Should().Be(123.45m);
            loaded.Nsu.Value.Should().Be(2);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string CreateXml() => $"""
        <NFSe xmlns="urn:neo-e:test">
          <chNFSe>{ValidKey}</chNFSe>
          <CNPJPrestador>{ValidCnpj}</CNPJPrestador>
          <CNPJTomador>{ValidCnpj}</CNPJTomador>
          <nNFSe>42</nNFSe>
          <serie>1</serie>
          <dhEmi>2026-09-23T10:00:00-03:00</dhEmi>
          <vLiq>123.45</vLiq>
        </NFSe>
        """;

    private static string CreateNfeXml(Cnpj emitter, Cnpj recipient)
    {
        var key = CreateNfeKey(emitter);
        return $"""
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
              <NFe>
                <infNFe Id="NFe{key}" versao="4.00">
                  <ide>
                    <nNF>42</nNF>
                    <serie>1</serie>
                    <dhEmi>2026-09-23T10:00:00-03:00</dhEmi>
                  </ide>
                  <emit><CNPJ>{emitter.Value}</CNPJ></emit>
                  <dest><CNPJ>{recipient.Value}</CNPJ></dest>
                  <total><ICMSTot><vNF>123.45</vNF></ICMSTot></total>
                </infNFe>
              </NFe>
              <protNFe>
                <infProt>
                  <dhRecbto>2026-09-23T10:01:00-03:00</dhRecbto>
                </infProt>
              </protNFe>
            </nfeProc>
            """;
    }

    private static string CreateNfeKey(Cnpj emitter)
        => $"35" +
           $"2609" +
           $"{emitter.Value}" +
           "55" +
           "001" +
           "000000042" +
           "1" +
           "12345678" +
           "0";

    private static string CompressXml(string xml)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(xml));
        return Convert.ToBase64String(output.ToArray());
    }
}