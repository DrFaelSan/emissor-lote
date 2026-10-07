using System.Globalization;
using NEO_e.Application.Contracts;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Application.UseCases;

public sealed class SimularCarteiraUseCase
{
    private const int DocumentsPerCompany = 2;
    private readonly IXmlParser _xmlParser;
    private readonly ISimulationXmlWriter _fileWriter;
    private readonly IProgressReporter _progress;

    public SimularCarteiraUseCase(
        IXmlParser xmlParser,
        ISimulationXmlWriter fileWriter,
        IProgressReporter progress)
    {
        _xmlParser = xmlParser;
        _fileWriter = fileWriter;
        _progress = progress;
    }

    public async Task<SimulationBatchResult> ExecuteAsync(
        IReadOnlyList<SimulationCompany> companies,
        bool includeExpectedFailure,
        CancellationToken ct)
    {
        var executionId = Guid.NewGuid();
        var results = new List<SimulationCompanyResult>();
        var companyIndex = 0;

        foreach (var company in companies)
        {
            ct.ThrowIfCancellationRequested();
            var startedAt = DateTimeOffset.Now;
            var processed = 0;
            var errors = 0;
            var initialNsu = new Nsu(0);
            var finalNsu = initialNsu;

            _progress.ReportEmpresaStart(company.Cnpj, initialNsu);

            try
            {
                for (var documentIndex = 1; documentIndex <= DocumentsPerCompany; documentIndex++)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        if (includeExpectedFailure && companyIndex == 0 && documentIndex == DocumentsPerCompany)
                            throw new IOException("Falha de gravacao configurada no cenario de teste.");

                        var nsu = new Nsu(documentIndex);
                        var xml = CreateSyntheticXml(company.Cnpj, documentIndex);
                        var document = _xmlParser.ParseNfse(xml, nsu);
                        await _fileWriter.WriteAsync(
                            executionId,
                            $"NFSe-simulada-{company.Cnpj.Value}-{documentIndex:D4}.xml",
                            xml,
                            ct);
                        processed++;
                        finalNsu = nsu;
                        _progress.ReportDocumentoProcessado(
                            company.Cnpj,
                            document.Nsu,
                            document.ChaveAcesso,
                            document.Tipo);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        errors++;
                        _progress.ReportWarning(
                            $"Falha simulada para {company.Name}, documento {documentIndex}: {exception.Message}");
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
                }

                _progress.ReportEmpresaComplete(company.Cnpj, processed, errors);
                results.Add(new SimulationCompanyResult(
                    company,
                    initialNsu,
                    finalNsu,
                    processed,
                    errors,
                    startedAt,
                    DateTimeOffset.Now));
            }
            catch (OperationCanceledException exception)
            {
                _progress.ReportEmpresaError(company.Cnpj, exception);
                throw;
            }

            companyIndex++;
        }

        return new SimulationBatchResult(executionId, results, _fileWriter.OutputFolder);
    }

    private static string CreateSyntheticXml(Cnpj companyCnpj, int documentIndex)
    {
        var accessKey = $"123456{companyCnpj.Value}{documentIndex:D30}";
        var issueDate = new DateTimeOffset(2026, 1, documentIndex, 10, 0, 0, TimeSpan.FromHours(-3));

        return $"""
            <NFSe xmlns="urn:neo-e:simulation">
              <chNFSe>{accessKey}</chNFSe>
              <CNPJPrestador>{companyCnpj.Value}</CNPJPrestador>
              <CNPJTomador>11222333000262</CNPJTomador>
              <nNFSe>{documentIndex.ToString(CultureInfo.InvariantCulture)}</nNFSe>
              <dhEmi>{issueDate:O}</dhEmi>
              <dhProc>{issueDate.AddMinutes(1):O}</dhProc>
              <vLiq>123.45</vLiq>
            </NFSe>
            """;
    }
}

public sealed record SimulationCompany(string Name, Cnpj Cnpj);

public sealed record SimulationCompanyResult(
    SimulationCompany Company,
    Nsu InitialNsu,
    Nsu FinalNsu,
    int DocumentsProcessed,
    int Errors,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);

public sealed record SimulationBatchResult(
    Guid ExecutionId,
    IReadOnlyList<SimulationCompanyResult> Companies,
    string OutputFolder);
