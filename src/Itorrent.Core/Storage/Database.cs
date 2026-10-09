using Microsoft.Data.Sqlite;

namespace Itorrent.Core.Storage;

/// <summary>
/// Abre conexões com o banco SQLite do app e cria o esquema.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;

    public Database(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        EnsureSchema();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS torrents (
                id            TEXT PRIMARY KEY,
                name          TEXT NOT NULL,
                save_path     TEXT NOT NULL,
                metadata      BLOB NOT NULL,
                extra_trackers TEXT NOT NULL DEFAULT '',
                skipped_files TEXT NOT NULL DEFAULT '',
                status        INTEGER NOT NULL,
                total_size    INTEGER NOT NULL,
                selected_size INTEGER NOT NULL,
                added_at      TEXT NOT NULL,
                completed_at  TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }
}
