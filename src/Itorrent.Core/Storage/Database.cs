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
                completed_at  TEXT NULL,
                uploaded_bytes   INTEGER NOT NULL DEFAULT 0,
                downloaded_bytes INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Bancos de versões antigas: acrescenta as colunas do contador de ratio.
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = conn.CreateCommand())
        {
            info.CommandText = "SELECT name FROM pragma_table_info('torrents')";
            using var r = info.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(0));
        }
        if (!columns.Contains("uploaded_bytes"))
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE torrents ADD COLUMN uploaded_bytes INTEGER NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();
        }
        if (!columns.Contains("downloaded_bytes"))
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE torrents ADD COLUMN downloaded_bytes INTEGER NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();
        }
    }
}
