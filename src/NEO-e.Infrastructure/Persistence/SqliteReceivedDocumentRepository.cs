using Microsoft.Data.Sqlite;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Infrastructure.Persistence;

public sealed class SqliteReceivedDocumentRepository : IReceivedDocumentRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SqliteReceivedDocumentRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<DocumentoRecebido?> GetByChaveAsync(ChaveAcesso chave, CancellationToken ct)
    {
        var rows = await QueryAsync("WHERE chave = $chave", [new("$chave", chave.Value)], ct);
        return rows.FirstOrDefault();
    }

    public Task<IReadOnlyList<DocumentoRecebido>> GetByCnpjAsync(Cnpj cnpj, CancellationToken ct)
        => QueryAsync("WHERE cnpj_destinatario = $cnpj ORDER BY data_emissao", [new("$cnpj", cnpj.Value)], ct);

    public Task<IReadOnlyList<DocumentoRecebido>> GetByCnpjAndPeriodoAsync(Cnpj cnpj, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
        => QueryAsync("WHERE cnpj_destinatario = $cnpj AND data_emissao >= $inicio AND data_emissao < $fim ORDER BY data_emissao",
            [new("$cnpj", cnpj.Value), new("$inicio", inicio.ToString("O")), new("$fim", fim.ToString("O"))], ct);

    public Task<IReadOnlyList<DocumentoRecebido>> GetByStatusAsync(Cnpj cnpj, StatusManifestacao status, CancellationToken ct)
        => QueryAsync("WHERE cnpj_destinatario = $cnpj AND status_manifestacao = $status ORDER BY data_emissao",
            [new("$cnpj", cnpj.Value), new("$status", status.ToString())], ct);

    public async Task SaveAsync(DocumentoRecebido documento, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var connection = await OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO documentos_recebidos (
                chave, tipo, cnpj_emitente, cnpj_destinatario, numero, serie,
                data_emissao, data_autorizacao, valor_total, xml_content,
                caminho, data_indexacao, nsu, status_manifestacao,
                data_manifestacao, protocolo_manifestacao, justificativa, hash_xml
            ) VALUES (
                $chave, $tipo, $emitente, $destinatario, $numero, $serie,
                $emissao, $autorizacao, $valor, $xml,
                $caminho, $indexacao, $nsu, $status,
                $manifestacao, $protocolo, $justificativa, $hash
            )
            ON CONFLICT(chave) DO UPDATE SET
                tipo = excluded.tipo,
                caminho = excluded.caminho,
                nsu = excluded.nsu,
                status_manifestacao = excluded.status_manifestacao,
                data_manifestacao = excluded.data_manifestacao,
                protocolo_manifestacao = excluded.protocolo_manifestacao,
                justificativa = excluded.justificativa
        """;
        command.Parameters.AddWithValue("$chave", documento.ChaveAcesso.Value);
        command.Parameters.AddWithValue("$tipo", documento.Tipo.ToString());
        command.Parameters.AddWithValue("$emitente", documento.CnpjEmitente.Value);
        command.Parameters.AddWithValue("$destinatario", documento.CnpjDestinatario.Value);
        command.Parameters.AddWithValue("$numero", documento.Numero);
        command.Parameters.AddWithValue("$serie", documento.Serie);
        command.Parameters.AddWithValue("$emissao", documento.DataEmissao.ToString("O"));
        command.Parameters.AddWithValue("$autorizacao", documento.DataAutorizacao?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$valor", documento.ValorTotal.Value);
        command.Parameters.AddWithValue("$xml", documento.XmlContent);
        command.Parameters.AddWithValue("$caminho", documento.CaminhoArquivo ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$indexacao", documento.DataIndexacao.ToString("O"));
        command.Parameters.AddWithValue("$nsu", documento.Nsu?.Value ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", documento.StatusManifestacao.ToString());
        command.Parameters.AddWithValue("$manifestacao", documento.DataManifestacao?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$protocolo", documento.ProtocoloManifestacao ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$justificativa", documento.Justificativa ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$hash", documento.HashXml);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveBatchAsync(IEnumerable<DocumentoRecebido> documentos, CancellationToken ct)
    {
        foreach (var documento in documentos)
            await SaveAsync(documento, ct);
    }

    private async Task<IReadOnlyList<DocumentoRecebido>> QueryAsync(string filter, IReadOnlyList<SqliteParameter> parameters, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var connection = await OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                chave, tipo, cnpj_emitente, cnpj_destinatario, numero, serie,
                data_emissao, data_autorizacao, valor_total, xml_content,
                caminho, data_indexacao, nsu, status_manifestacao,
                data_manifestacao, protocolo_manifestacao, justificativa, hash_xml
            FROM documentos_recebidos {filter}
        """;
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        var results = new List<DocumentoRecebido>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) results.Add(ReadDocument(reader));
        return results;
    }

    private static DocumentoRecebido ReadDocument(SqliteDataReader reader)
    {
        var documento = new DocumentoRecebido(
            ChaveAcesso.Parse(reader.GetString(0)),
            Enum.Parse<TipoDocumentoFiscal>(reader.GetString(1)),
            Cnpj.Parse(reader.GetString(2)),
            Cnpj.Parse(reader.GetString(3)),
            reader.GetString(4),
            reader.GetString(5),
            DateTimeOffset.Parse(reader.GetString(6)),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
            new ValorMonetario(reader.GetDecimal(8)),
            reader.GetString(9));

        if (!reader.IsDBNull(10)) documento.CaminhoArquivo = reader.GetString(10);
        documento.DataIndexacao = DateTimeOffset.Parse(reader.GetString(11));
        if (!reader.IsDBNull(12)) documento.Nsu = new Nsu(reader.GetInt64(12));
        documento.StatusManifestacao = Enum.Parse<StatusManifestacao>(reader.GetString(13));
        if (!reader.IsDBNull(14)) documento.DataManifestacao = DateTimeOffset.Parse(reader.GetString(14));
        if (!reader.IsDBNull(15)) documento.ProtocoloManifestacao = reader.GetString(15);
        if (!reader.IsDBNull(16)) documento.Justificativa = reader.GetString(16);

        return documento;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var connection = await OpenAsync(ct);
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS documentos_recebidos (
                    chave TEXT PRIMARY KEY NOT NULL,
                    tipo TEXT NOT NULL,
                    cnpj_emitente TEXT NOT NULL,
                    cnpj_destinatario TEXT NOT NULL,
                    numero TEXT NOT NULL,
                    serie TEXT NOT NULL,
                    data_emissao TEXT NOT NULL,
                    data_autorizacao TEXT NULL,
                    valor_total NUMERIC NOT NULL,
                    xml_content TEXT NOT NULL,
                    caminho TEXT NULL,
                    data_indexacao TEXT NOT NULL,
                    nsu INTEGER NULL,
                    status_manifestacao TEXT NOT NULL DEFAULT 'Pendente',
                    data_manifestacao TEXT NULL,
                    protocolo_manifestacao TEXT NULL,
                    justificativa TEXT NULL,
                    hash_xml TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_doc_rec_destinatario_data ON documentos_recebidos(cnpj_destinatario, data_emissao);
                CREATE INDEX IF NOT EXISTS idx_doc_rec_status ON documentos_recebidos(cnpj_destinatario, status_manifestacao);
                CREATE INDEX IF NOT EXISTS idx_doc_rec_nsu ON documentos_recebidos(cnpj_destinatario, nsu);
            """;
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _initLock.Release(); }
    }
}