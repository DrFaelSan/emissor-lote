using System.IO.Compression;
using ClosedXML.Excel;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Infrastructure.Export;

public sealed class ClosedXmlExcelExporter : IExcelExporter
{
    public async Task<byte[]> ExportExecutionAsync(IReadOnlyList<ExecutionRecord> records, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Execução");

            // Cabeçalho
            var headers = new[]
            {
                "CNPJ", "Empresa", "NSU Inicial", "NSU Final", "Documentos Processados",
                "Erros", "Duração (ms)", "Início", "Fim", "Status"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D4AF37");
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Dados
            for (int row = 0; row < records.Count; row++)
            {
                var r = records[row];
                var dataRow = row + 2;
                worksheet.Cell(dataRow, 1).Value = r.Cnpj.Format();
                worksheet.Cell(dataRow, 2).Value = r.Empresa;
                worksheet.Cell(dataRow, 3).Value = r.NsuInicial.Value;
                worksheet.Cell(dataRow, 4).Value = r.NsuFinal.Value;
                worksheet.Cell(dataRow, 5).Value = r.DocumentosProcessados;
                worksheet.Cell(dataRow, 6).Value = r.Erros;
                worksheet.Cell(dataRow, 7).Value = r.Duracao.TotalMilliseconds;
                worksheet.Cell(dataRow, 8).Value = r.Inicio.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(dataRow, 9).Value = r.Fim?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                worksheet.Cell(dataRow, 10).Value = r.Status;

                // Colorir linha de erro
                if (r.Erros > 0)
                {
                    var rowRange = worksheet.Range(dataRow, 1, dataRow, headers.Length);
                    rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3E0");
                }
            }

            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }, ct);
    }

    public async Task<byte[]> ExportInventoryAsync(IReadOnlyList<InventoryRecord> records, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Inventário");

            var headers = new[]
            {
                "CNPJ", "Chave de Acesso", "Tipo", "Número", "Série",
                "Data Emissão", "Valor", "Caminho Arquivo", "NSU"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D4AF37");
                cell.Style.Font.FontColor = XLColor.White;
            }

            for (int row = 0; row < records.Count; row++)
            {
                var r = records[row];
                var dataRow = row + 2;
                worksheet.Cell(dataRow, 1).Value = r.Cnpj.Format();
                worksheet.Cell(dataRow, 2).Value = r.ChaveAcesso.Value;
                worksheet.Cell(dataRow, 3).Value = r.Tipo.ToString();
                worksheet.Cell(dataRow, 4).Value = r.Numero;
                worksheet.Cell(dataRow, 5).Value = r.Serie;
                worksheet.Cell(dataRow, 6).Value = r.DataEmissao.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(dataRow, 7).Value = r.Valor.Value;
                worksheet.Cell(dataRow, 8).Value = r.CaminhoArquivo ?? "";
                worksheet.Cell(dataRow, 9).Value = r.Nsu.Value;

                // Formatar valor como moeda
                worksheet.Cell(dataRow, 7).Style.NumberFormat.Format = "R$ #,##0.00";
            }

            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }, ct);
    }
}

// Implementação adicional para exportação de documentos recebidos (RFC-002)
public sealed class ClosedXmlReceivedDocumentExporter
{
    public byte[] ExportReceivedDocuments(IReadOnlyList<DocumentoRecebido> documents, bool includeDivergencesOnly = false)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Documentos Recebidos");

        var headers = new[]
        {
            "CNPJ Destinatário", "CNPJ Emitente", "Chave de Acesso", "Tipo", "Número", "Série",
            "Data Emissão", "Data Autorização", "Valor Total", "Status Manifestação",
            "Data Manifestação", "Protocolo", "Justificativa", "Caminho Arquivo"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D4AF37");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int dataRow = 2;
        foreach (var doc in documents)
        {
            if (includeDivergencesOnly && doc.StatusManifestacao == StatusManifestacao.Pendente)
                continue;

            worksheet.Cell(dataRow, 1).Value = doc.CnpjDestinatario.Format();
            worksheet.Cell(dataRow, 2).Value = doc.CnpjEmitente.Format();
            worksheet.Cell(dataRow, 3).Value = doc.ChaveAcesso.Value;
            worksheet.Cell(dataRow, 4).Value = doc.Tipo.ToString();
            worksheet.Cell(dataRow, 5).Value = doc.Numero;
            worksheet.Cell(dataRow, 6).Value = doc.Serie;
            worksheet.Cell(dataRow, 7).Value = doc.DataEmissao.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(dataRow, 8).Value = doc.DataAutorizacao?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            worksheet.Cell(dataRow, 9).Value = doc.ValorTotal.Value;
            worksheet.Cell(dataRow, 10).Value = doc.StatusManifestacao.ToString();
            worksheet.Cell(dataRow, 11).Value = doc.DataManifestacao?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            worksheet.Cell(dataRow, 12).Value = doc.ProtocoloManifestacao ?? "";
            worksheet.Cell(dataRow, 13).Value = doc.Justificativa ?? "";
            worksheet.Cell(dataRow, 14).Value = doc.CaminhoArquivo ?? "";

            // Formatar valor
            worksheet.Cell(dataRow, 9).Style.NumberFormat.Format = "R$ #,##0.00";

            // Colorir por status
            var statusColor = doc.StatusManifestacao switch
            {
                StatusManifestacao.ConfirmacaoDaOperacao => XLColor.FromHtml("#E8F5E9"), // Verde claro
                StatusManifestacao.DesconhecimentoDaOperacao => XLColor.FromHtml("#FFEBEE"), // Vermelho claro
                StatusManifestacao.OperacaoNaoRealizada => XLColor.FromHtml("#FFF3E0"), // Laranja claro
                StatusManifestacao.CienciaDaOperacao => XLColor.FromHtml("#E3F2FD"), // Azul claro
                StatusManifestacao.ErroEnvio => XLColor.FromHtml("#FFEBEE"),
                StatusManifestacao.PrazoExpirado => XLColor.FromHtml("#FFEBEE"),
                StatusManifestacao.Duplicidade => XLColor.FromHtml("#FFF3E0"),
                _ => XLColor.White
            };

            var rowRange = worksheet.Range(dataRow, 1, dataRow, headers.Length);
            rowRange.Style.Fill.BackgroundColor = statusColor;

            dataRow++;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportDivergences(IReadOnlyList<DocumentoRecebido> documents, IReadOnlyList<ValorReferenciaMatch> matches)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Divergências");

        var headers = new[]
        {
            "CNPJ Emitente", "Chave de Acesso", "Número", "Série", "Data Emissão",
            "Valor Nota", "Valor Referência", "Delta", "Delta %", "Nível Match", "Observação"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D4AF37");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int dataRow = 2;
        foreach (var match in matches.Where(m => m.Classificacao == MatchClassificacao.Divergente))
        {
            var doc = documents.FirstOrDefault(d => d.ChaveAcesso.Value == match.ChaveAcesso);
            if (doc == null) continue;

            worksheet.Cell(dataRow, 1).Value = doc.CnpjEmitente.Format();
            worksheet.Cell(dataRow, 2).Value = doc.ChaveAcesso.Value;
            worksheet.Cell(dataRow, 3).Value = doc.Numero;
            worksheet.Cell(dataRow, 4).Value = doc.Serie;
            worksheet.Cell(dataRow, 5).Value = doc.DataEmissao.ToString("yyyy-MM-dd");
            worksheet.Cell(dataRow, 6).Value = doc.ValorTotal.Value;
            worksheet.Cell(dataRow, 7).Value = match.ValorReferencia;
            worksheet.Cell(dataRow, 8).Value = match.Delta;
            worksheet.Cell(dataRow, 9).Value = match.DeltaPercentual;
            worksheet.Cell(dataRow, 10).Value = match.NivelMatch.ToString();
            worksheet.Cell(dataRow, 11).Value = match.Observacao;

            worksheet.Cell(dataRow, 6).Style.NumberFormat.Format = "R$ #,##0.00";
            worksheet.Cell(dataRow, 7).Style.NumberFormat.Format = "R$ #,##0.00";
            worksheet.Cell(dataRow, 8).Style.NumberFormat.Format = "R$ #,##0.00";
            worksheet.Cell(dataRow, 9).Style.NumberFormat.Format = "0.00%";

            // Destacar divergência
            var rowRange = worksheet.Range(dataRow, 1, dataRow, headers.Length);
            rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEBEE");

            dataRow++;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

// DTOs de apoio para RFC-002 (podem ser movidos para Application.Contracts depois)
public sealed record ValorReferenciaMatch(
    string ChaveAcesso,
    decimal ValorNota,
    decimal ValorReferencia,
    decimal Delta,
    decimal DeltaPercentual,
    NivelMatch NivelMatch,
    MatchClassificacao Classificacao,
    string Observacao);

public enum NivelMatch
{
    ChaveExata,
    CnpjNumeroSerie,
    CnpjDataValor,
    CnpjPeriodoSoma,
    NaoEncontrado
}

public enum MatchClassificacao
{
    OK,
    Divergente,
    SemReferencia,
    Ambiguidade
}

public sealed record ValorReferencia(
    string CnpjEmitente,
    decimal Valor,
    DateTimeOffset Data,
    string? ChaveAcesso = null,
    string? Numero = null,
    string? Serie = null,
    string? Observacao = null);