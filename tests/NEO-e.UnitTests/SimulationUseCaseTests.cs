using FluentAssertions;
using NEO_e.Application.Contracts;
using NEO_e.Application.UseCases;
using NEO_e.Domain.ValueObjects;
using NEO_e.Infrastructure.Parsers;
using NEO_e.Infrastructure.FileSystem;
using NEO_e.Domain.Entities;

namespace NEO_e.UnitTests;

public sealed class SimulationUseCaseTests
{
    private const string CnpjValue = "11222333000181";

    [Fact]
    public async Task Simulation_writes_synthetic_documents_to_an_isolated_execution_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-simulation-{Guid.NewGuid():N}");
        try
        {
            var progress = new RecordingProgressReporter();
            var writer = new SimulationXmlFileWriter(root);
            var useCase = new SimularCarteiraUseCase(new NfseXmlParser(), writer, progress);
            var company = new SimulationCompany("Empresa Simulada", Cnpj.Parse(CnpjValue));

            var result = await useCase.ExecuteAsync([company], false, CancellationToken.None);

            progress.Warnings.Should().BeEmpty();
            result.Companies.Should().ContainSingle();
            result.Companies[0].DocumentsProcessed.Should().Be(2);
            result.Companies[0].Errors.Should().Be(0);
            result.OutputFolder.Should().Be(root);
            progress.ProcessedDocuments.Should().Be(2);
            Directory.GetFiles(Path.Combine(root, result.ExecutionId.ToString("N")), "*.xml").Should().HaveCount(2);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Simulation_failure_is_reported_and_does_not_fake_a_successful_document()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-simulation-{Guid.NewGuid():N}");
        try
        {
            var progress = new RecordingProgressReporter();
            var writer = new SimulationXmlFileWriter(root);
            var useCase = new SimularCarteiraUseCase(new NfseXmlParser(), writer, progress);
            var company = new SimulationCompany("Empresa Simulada", Cnpj.Parse(CnpjValue));

            var result = await useCase.ExecuteAsync([company], true, CancellationToken.None);

            result.Companies[0].DocumentsProcessed.Should().Be(1);
            result.Companies[0].Errors.Should().Be(1);
            progress.ProcessedDocuments.Should().Be(1);
            progress.Warnings.Should().ContainSingle();
            Directory.GetFiles(Path.Combine(root, result.ExecutionId.ToString("N")), "*.xml").Should().HaveCount(1);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Cancelled_simulation_file_write_leaves_no_partial_xml()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-e-simulation-{Guid.NewGuid():N}");
        try
        {
            var writer = new SimulationXmlFileWriter(root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var write = () => writer.WriteAsync(
                Guid.NewGuid(),
                "NFSe-simulada.xml",
                "<NFSe />",
                cancellation.Token);

            await write.Should().ThrowAsync<OperationCanceledException>();
            Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories).Should().BeEmpty();
            Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private sealed class RecordingProgressReporter : IProgressReporter
    {
        public int ProcessedDocuments { get; private set; }
        public List<string> Warnings { get; } = [];

        public void ReportEmpresaStart(Cnpj cnpj, Nsu nsuInicial)
        {
        }

        public void ReportDocumentoProcessado(Cnpj cnpj, Nsu nsu, ChaveAcesso chave, TipoDocumento tipo)
        {
            ProcessedDocuments++;
        }

        public void ReportEmpresaComplete(Cnpj cnpj, int documentosProcessados, int erros)
        {
        }

        public void ReportEmpresaError(Cnpj cnpj, Exception erro)
        {
        }

        public void ReportProgress(string mensagem)
        {
        }

        public void ReportWarning(string mensagem)
        {
            Warnings.Add(mensagem);
        }
    }
}
