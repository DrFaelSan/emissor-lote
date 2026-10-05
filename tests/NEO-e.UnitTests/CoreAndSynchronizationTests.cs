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

    private static string CompressXml(string xml)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(xml));
        return Convert.ToBase64String(output.ToArray());
    }
}