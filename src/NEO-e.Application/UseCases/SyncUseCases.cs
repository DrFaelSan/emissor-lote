using System.Security.Cryptography.X509Certificates;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;

namespace NEO_e.Application.UseCases;

public sealed class SincronizarEmpresaUseCase
{
    private readonly IAdnClient _adnClient;
    private readonly INsuRepository _nsuRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IXmlParser _xmlParser;
    private readonly IFileWriter _fileWriter;
    private readonly IProgressReporter _progress;

    public SincronizarEmpresaUseCase(
        IAdnClient adnClient,
        INsuRepository nsuRepository,
        IDocumentRepository documentRepository,
        IXmlParser xmlParser,
        IFileWriter fileWriter,
        IProgressReporter progress)
    {
        _adnClient = adnClient;
        _nsuRepository = nsuRepository;
        _documentRepository = documentRepository;
        _xmlParser = xmlParser;
        _fileWriter = fileWriter;
        _progress = progress;
    }

    public async Task<EmpresaSyncResult> ExecuteAsync(
        Empresa empresa,
        X509Certificate2 certificate,
        bool resetNsu,
        CancellationToken ct)
    {
        var estado = await _nsuRepository.GetAsync(empresa.Cnpj, ct);

        if (resetNsu || estado is null)
        {
            estado = EstadoSincronizacao.Create(empresa.Cnpj);
        }

        var nsuInicial = estado.UltimoNsuConfirmado;
        var documentosProcessados = 0;
        var erros = 0;

        _progress.ReportEmpresaStart(empresa.Cnpj, nsuInicial);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                ct.ThrowIfCancellationRequested();

                var nsuAnterior = estado.UltimoNsuConfirmado;
                var response = await _adnClient.GetDfeAsync(empresa.Cnpj, estado.UltimoNsuConfirmado, certificate, ct);

                if (response.Lote.Count == 0)
                {
                    _progress.ReportProgress($"CNPJ {empresa.Cnpj.Format()}: Caixa vazia, sincronização concluída");
                    break;
                }

                estado.AtualizarProgresso(response.UltNsu, response.MaxNsu);

                var documentos = _xmlParser.ParseLote(response.Lote).ToList();

                foreach (var doc in documentos)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        doc.ClassificarPara(empresa.Cnpj);

                        var existe = await _documentRepository.GetByChaveAsync(doc.ChaveAcesso, ct);
                        if (existe is not null && existe.ConteudoIgual(doc.XmlContent))
                        {
                            _progress.ReportProgress($"CNPJ {empresa.Cnpj.Format()}: Documento {doc.ChaveAcesso} já existe, ignorando");
                            continue;
                        }

                        var caminho = await _fileWriter.WriteAsync(doc, ct);
                        doc.DefinirCaminhoArquivo(caminho);

                        await _documentRepository.SaveAsync(doc, ct);
                        documentosProcessados++;

                        _progress.ReportDocumentoProcessado(empresa.Cnpj, doc.Nsu, doc.ChaveAcesso, doc.Tipo);
                    }
                    catch (Exception ex)
                    {
                        erros++;
                        _progress.ReportWarning($"Erro ao processar documento {doc.ChaveAcesso}: {ex.Message}");
                    }
                }

                if (erros > 0)
                {
                    throw new SynchronizationException(
                        $"Lote não confirmado porque {erros} documento(s) falharam",
                        SyncErrorCode.WriteFailed,
                        empresa.Cnpj.ToString(),
                        response.UltNsu.Value);
                }

                estado.ConfirmarNsu(response.UltNsu);
                await _nsuRepository.SaveAsync(estado, ct);

                if (response.UltNsu >= response.MaxNsu)
                {
                    _progress.ReportProgress($"CNPJ {empresa.Cnpj.Format()}: NSU máximo atingido ({response.MaxNsu})");
                    break;
                }

                if (response.UltNsu <= nsuAnterior)
                {
                    throw new SynchronizationException(
                        $"API não progrediu: NSU {estado.UltimoNsuConfirmado} -> {response.UltNsu}",
                        SyncErrorCode.NoProgress,
                        empresa.Cnpj.ToString(),
                        response.UltNsu.Value);
                }
            }

            empresa.AtualizarNsu(estado.UltimoNsuConfirmado);
            empresa.AtualizarStatus(erros == 0 ? EmpresaStatus.Sucesso : EmpresaStatus.ErroSincronizacao,
                erros > 0 ? $"{erros} erro(s) durante processamento" : null);

            _progress.ReportEmpresaComplete(empresa.Cnpj, documentosProcessados, erros);

            return new EmpresaSyncResult(
                empresa.Cnpj,
                true,
                documentosProcessados,
                erros,
                estado.UltimoNsuConfirmado,
                null);
        }
        catch (OperationCanceledException)
        {
            empresa.AtualizarStatus(EmpresaStatus.Cancelada, "Cancelado pelo usuário");
            _progress.ReportEmpresaError(empresa.Cnpj, new OperationCanceledException("Sincronização cancelada"));

            return new EmpresaSyncResult(
                empresa.Cnpj,
                false,
                documentosProcessados,
                erros + 1,
                estado.UltimoNsuConfirmado,
                "Cancelado");
        }
        catch (Exception ex)
        {
            empresa.AtualizarStatus(EmpresaStatus.ErroSincronizacao, ex.Message);
            _progress.ReportEmpresaError(empresa.Cnpj, ex);

            return new EmpresaSyncResult(
                empresa.Cnpj,
                false,
                documentosProcessados,
                erros + 1,
                estado.UltimoNsuConfirmado,
                ex.Message);
        }
    }
}

public sealed record EmpresaSyncResult(
    Cnpj Cnpj,
    bool Sucesso,
    int DocumentosProcessados,
    int Erros,
    Nsu UltimoNsu,
    string? Erro);

public sealed class ResetNsuUseCase
{
    private readonly INsuRepository _nsuRepository;
    private readonly IProgressReporter _progress;

    public ResetNsuUseCase(INsuRepository nsuRepository, IProgressReporter progress)
    {
        _nsuRepository = nsuRepository;
        _progress = progress;
    }

    public async Task<ResetNsuResult> ExecuteAsync(IReadOnlyList<Empresa> empresas, CancellationToken ct)
    {
        var sucessos = 0;
        var falhas = 0;
        var erros = new List<string>();

        foreach (var empresa in empresas.Where(e => e.Selecionada))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await _nsuRepository.DeleteAsync(empresa.Cnpj, ct);
                empresa.ResetarNsu();
                _progress.ReportProgress($"NSU resetado para {empresa.Nome} ({empresa.Cnpj.Format()})");
                sucessos++;
            }
            catch (Exception ex)
            {
                falhas++;
                erros.Add($"{empresa.Nome}: {ex.Message}");
                _progress.ReportWarning($"Erro ao resetar NSU para {empresa.Nome}: {ex.Message}");
            }
        }

        return new ResetNsuResult(sucessos, falhas, erros);
    }
}

public sealed record ResetNsuResult(
    int Sucessos,
    int Falhas,
    IReadOnlyList<string> Erros);

public sealed class SincronizarCarteiraUseCase
{
    private readonly SincronizarEmpresaUseCase _sincronizarEmpresa;
    private readonly IProgressReporter _progress;

    public SincronizarCarteiraUseCase(
        SincronizarEmpresaUseCase sincronizarEmpresa,
        IProgressReporter progress)
    {
        _sincronizarEmpresa = sincronizarEmpresa;
        _progress = progress;
    }

    public async Task<CarteiraSyncResult> ExecuteAsync(
        IReadOnlyList<Empresa> empresas,
        Func<Empresa, Task<X509Certificate2?>> getCertificate,
        bool resetNsu,
        CancellationToken ct)
    {
        var resultados = new List<EmpresaSyncResult>();
        var totalEmpresas = empresas.Count(e => e.Selecionada);
        var processadas = 0;

        foreach (var empresa in empresas.Where(e => e.Selecionada))
        {
            ct.ThrowIfCancellationRequested();

            processadas++;
            _progress.ReportProgress($"[{processadas}/{totalEmpresas}] Iniciando {empresa.Nome} ({empresa.Cnpj.Format()})");

            var certificate = await getCertificate(empresa);
            if (certificate is null)
            {
                empresa.AtualizarStatus(EmpresaStatus.ErroCertificado, "Certificado não carregado");
                resultados.Add(new EmpresaSyncResult(
                    empresa.Cnpj, false, 0, 1, Nsu.Zero, "Certificado não disponível"));
                continue;
            }

            var resultado = await _sincronizarEmpresa.ExecuteAsync(empresa, certificate, resetNsu, ct);
            resultados.Add(resultado);

            if (ct.IsCancellationRequested)
                break;
        }

        var sucessos = resultados.Count(r => r.Sucesso);
        var falhas = resultados.Count(r => !r.Sucesso);
        var totalDocs = resultados.Sum(r => r.DocumentosProcessados);
        var totalErros = resultados.Sum(r => r.Erros);

        _progress.ReportProgress($"Sincronização concluída: {sucessos} sucesso(s), {falhas} falha(s), {totalDocs} documento(s), {totalErros} erro(s)");

        return new CarteiraSyncResult(resultados, sucessos, falhas, totalDocs, totalErros);
    }
}

public sealed record CarteiraSyncResult(
    IReadOnlyList<EmpresaSyncResult> Resultados,
    int Sucessos,
    int Falhas,
    int TotalDocumentos,
    int TotalErros);