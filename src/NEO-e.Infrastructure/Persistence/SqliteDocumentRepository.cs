using Microsoft.Data.Sqlite;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;

namespace NEO_e.Infrastructure.Persistence;

public sealed class SqliteDocumentRepository : IDocumentRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SqliteDocumentRepository(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    public async Task<DocumentoFiscal?> GetByChaveAsync(ChaveAcesso chave, CancellationToken ct)
    {
        var rows = await QueryAsync("WHERE chave = $chave", [new("$chave", chave.Value)], ct);
        return rows.FirstOrDefault();
    }

    public Task<IReadOnlyList<DocumentoFiscal>> GetByCnpjAsync(Cnpj cnpj, CancellationToken ct) => QueryAsync("WHERE cnpj_destinatario = $cnpj ORDER BY data_emissao", [new("$cnpj", cnpj.Value)], ct);

    public Task<IReadOnlyList<DocumentoFiscal>> GetByCnpjAndPeriodoAsync(Cnpj cnpj, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct) => QueryAsync("WHERE cnpj_destinatario = $cnpj AND data_emissao >= $inicio AND data_emissao < $fim ORDER BY data_emissao", [new("$cnpj", cnpj.Value), new("$inicio", inicio.ToString("O")), new("$fim", fim.ToString("O"))], ct);

    public async Task SaveAsync(DocumentoFiscal documento, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var connection = await OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO documentos (chave, tipo, cnpj_emitente, cnpj_destinatario, numero, serie, data_emissao, data_autorizacao, valor_total, xml_content, nsu, caminho) VALUES ($chave, $tipo, $emitente, $destinatario, $numero, $serie, $emissao, $autorizacao, $valor, $xml, $nsu, $caminho) ON CONFLICT(chave) DO UPDATE SET tipo = excluded.tipo, caminho = excluded.caminho, nsu = excluded.nsu";
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
        command.Parameters.AddWithValue("$nsu", documento.Nsu.Value);
        command.Parameters.AddWithValue("$caminho", documento.CaminhoArquivo ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveBatchAsync(IEnumerable<DocumentoFiscal> documentos, CancellationToken ct)
    {
        foreach (var documento in documentos) await SaveAsync(documento, ct);
    }

    private async Task<IReadOnlyList<DocumentoFiscal>> QueryAsync(string filter, IReadOnlyList<SqliteParameter> parameters, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var connection = await OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT chave, tipo, cnpj_emitente, cnpj_destinatario, numero, serie, data_emissao, data_autorizacao, valor_total, xml_content, nsu, caminho FROM documentos {filter}";
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        var results = new List<DocumentoFiscal>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) results.Add(ReadDocument(reader));
        return results;
    }

    private static DocumentoFiscal ReadDocument(SqliteDataReader reader)
    {
        var documento = new DocumentoFiscal(ChaveAcesso.Parse(reader.GetString(0)), Enum.Parse<TipoDocumento>(reader.GetString(1)), Cnpj.Parse(reader.GetString(2)), Cnpj.Parse(reader.GetString(3)), reader.GetString(4), reader.GetString(5), DateTimeOffset.Parse(reader.GetString(6)), reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)), new ValorMonetario(reader.GetDecimal(8)), reader.GetString(9), new Nsu(reader.GetInt64(10)));
        if (!reader.IsDBNull(11)) documento.DefinirCaminhoArquivo(reader.GetString(11));
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
            command.CommandText = "CREATE TABLE IF NOT EXISTS documentos (chave TEXT PRIMARY KEY, tipo TEXT NOT NULL, cnpj_emitente TEXT NOT NULL, cnpj_destinatario TEXT NOT NULL, numero TEXT NOT NULL, serie TEXT NOT NULL, data_emissao TEXT NOT NULL, data_autorizacao TEXT NULL, valor_total NUMERIC NOT NULL, xml_content TEXT NOT NULL, nsu INTEGER NOT NULL, caminho TEXT NULL); CREATE INDEX IF NOT EXISTS idx_documentos_destinatario_data ON documentos(cnpj_destinatario, data_emissao);";
            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally { _initLock.Release(); }
    }
}