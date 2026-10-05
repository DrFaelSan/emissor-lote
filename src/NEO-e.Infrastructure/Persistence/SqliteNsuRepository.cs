using Microsoft.Data.Sqlite;
using NEO_e.Application.Contracts;
using NEO_e.Domain.Entities;
using NEO_e.Domain.ValueObjects;
using NEO_e.Domain.Exceptions;

namespace NEO_e.Infrastructure.Persistence;

public sealed class SqliteNsuRepository : INsuRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SqliteNsuRepository(string databasePath)
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

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS estado_sincronizacao (
                    cnpj TEXT PRIMARY KEY NOT NULL,
                    ultimo_nsu_confirmado INTEGER NOT NULL DEFAULT 0,
                    ultimo_nsu_consultado INTEGER NOT NULL DEFAULT 0,
                    max_nsu INTEGER NOT NULL DEFAULT 0,
                    ultima_sincronizacao TEXT NOT NULL,
                    versao INTEGER NOT NULL DEFAULT 1,
                    data_criacao TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_estado_ultima_sync ON estado_sincronizacao(ultima_sincronizacao);
            """;
            await cmd.ExecuteNonQueryAsync(ct);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<EstadoSincronizacao?> GetAsync(Cnpj cnpj, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT cnpj, ultimo_nsu_confirmado, ultimo_nsu_consultado, max_nsu, ultima_sincronizacao, versao, data_criacao FROM estado_sincronizacao WHERE cnpj = $cnpj";
        cmd.Parameters.AddWithValue("$cnpj", cnpj.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var estado = EstadoSincronizacao.Create(
            Cnpj.Parse(reader.GetString(0)),
            new Nsu(reader.GetInt64(1)),
            reader.GetInt64(3),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.GetInt32(5));

        var consultado = new Nsu(reader.GetInt64(2));
        if (consultado > estado.UltimoNsuConsultado)
        {
            typeof(EstadoSincronizacao)
                .GetProperty(nameof(EstadoSincronizacao.UltimoNsuConsultado))!
                .SetValue(estado, consultado);
        }

        return estado;
    }

    public async Task SaveAsync(EstadoSincronizacao estado, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO estado_sincronizacao (cnpj, ultimo_nsu_confirmado, ultimo_nsu_consultado, max_nsu, ultima_sincronizacao, versao, data_criacao)
                VALUES ($cnpj, $ultimo_nsu_confirmado, $ultimo_nsu_consultado, $max_nsu, $ultima_sincronizacao, $versao, $data_criacao)
                ON CONFLICT(cnpj) DO UPDATE SET
                    ultimo_nsu_confirmado = $ultimo_nsu_confirmado,
                    ultimo_nsu_consultado = $ultimo_nsu_consultado,
                    max_nsu = $max_nsu,
                    ultima_sincronizacao = $ultima_sincronizacao,
                    versao = $versao
            """;
            cmd.Parameters.AddWithValue("$cnpj", estado.Cnpj.Value);
            cmd.Parameters.AddWithValue("$ultimo_nsu_confirmado", estado.UltimoNsuConfirmado.Value);
            cmd.Parameters.AddWithValue("$ultimo_nsu_consultado", estado.UltimoNsuConsultado.Value);
            cmd.Parameters.AddWithValue("$max_nsu", estado.MaxNsu);
            cmd.Parameters.AddWithValue("$ultima_sincronizacao", estado.UltimaSincronizacao.ToString("O"));
            cmd.Parameters.AddWithValue("$versao", estado.Versao);
            cmd.Parameters.AddWithValue("$data_criacao", estado.DataCriacao.ToString("O"));

            await cmd.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteAsync(Cnpj cnpj, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM estado_sincronizacao WHERE cnpj = $cnpj";
        cmd.Parameters.AddWithValue("$cnpj", cnpj.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<EstadoSincronizacao>> GetAllAsync(CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT cnpj, ultimo_nsu_confirmado, ultimo_nsu_consultado, max_nsu, ultima_sincronizacao, versao, data_criacao FROM estado_sincronizacao";

        var results = new List<EstadoSincronizacao>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var estado = EstadoSincronizacao.Create(
                Cnpj.Parse(reader.GetString(0)),
                new Nsu(reader.GetInt64(1)),
                reader.GetInt64(3),
                DateTimeOffset.Parse(reader.GetString(4)),
                reader.GetInt32(5));

            var consultado = new Nsu(reader.GetInt64(2));
            if (consultado > estado.UltimoNsuConsultado)
            {
                typeof(EstadoSincronizacao)
                    .GetProperty(nameof(EstadoSincronizacao.UltimoNsuConsultado))!
                    .SetValue(estado, consultado);
            }

            results.Add(estado);
        }

        return results;
    }
}