using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Itorrent.Core.Storage;

public enum StoredStatus
{
    Active = 0,
    Paused = 1,
    Completed = 2,
}

public sealed record TorrentRecord(
    string Id,
    string Name,
    string SavePath,
    byte[] Metadata,
    IReadOnlyList<string> ExtraTrackers,
    IReadOnlySet<int> SkippedFiles,
    StoredStatus Status,
    long TotalSize,
    long SelectedSize,
    DateTimeOffset AddedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Lista de torrents. Sempre com consultas parametrizadas (nome de torrent é entrada não confiável).
/// </summary>
public sealed class TorrentRepository(Database db)
{
    public IReadOnlyList<TorrentRecord> GetAll()
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, save_path, metadata, extra_trackers, skipped_files, status,
                   total_size, selected_size, added_at, completed_at
            FROM torrents ORDER BY added_at
            """;
        using var r = cmd.ExecuteReader();
        var list = new List<TorrentRecord>();
        while (r.Read())
        {
            list.Add(new TorrentRecord(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                (byte[])r.GetValue(3),
                SplitLines(r.GetString(4)),
                ParseIndexes(r.GetString(5)),
                (StoredStatus)r.GetInt32(6),
                r.GetInt64(7),
                r.GetInt64(8),
                DateTimeOffset.Parse(r.GetString(9), CultureInfo.InvariantCulture),
                r.IsDBNull(10) ? null : DateTimeOffset.Parse(r.GetString(10), CultureInfo.InvariantCulture)));
        }
        return list;
    }

    public void Upsert(TorrentRecord t)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO torrents (id, name, save_path, metadata, extra_trackers, skipped_files, status,
                                  total_size, selected_size, added_at, completed_at)
            VALUES ($id, $name, $save, $meta, $trackers, $skipped, $status, $total, $selected, $added, $completed)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name, save_path = excluded.save_path, metadata = excluded.metadata,
                extra_trackers = excluded.extra_trackers, skipped_files = excluded.skipped_files,
                status = excluded.status, total_size = excluded.total_size,
                selected_size = excluded.selected_size, completed_at = excluded.completed_at
            """;
        cmd.Parameters.AddWithValue("$id", t.Id);
        cmd.Parameters.AddWithValue("$name", t.Name);
        cmd.Parameters.AddWithValue("$save", t.SavePath);
        cmd.Parameters.Add("$meta", SqliteType.Blob).Value = t.Metadata;
        cmd.Parameters.AddWithValue("$trackers", string.Join('\n', t.ExtraTrackers));
        cmd.Parameters.AddWithValue("$skipped", string.Join(',', t.SkippedFiles.Order()));
        cmd.Parameters.AddWithValue("$status", (int)t.Status);
        cmd.Parameters.AddWithValue("$total", t.TotalSize);
        cmd.Parameters.AddWithValue("$selected", t.SelectedSize);
        cmd.Parameters.AddWithValue("$added", t.AddedAt.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$completed",
            (object?)t.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void SetStatus(string id, StoredStatus status, DateTimeOffset? completedAt = null)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE torrents SET status = $status, completed_at = COALESCE($completed, completed_at) WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$status", (int)status);
        cmd.Parameters.AddWithValue("$completed",
            (object?)completedAt?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void Delete(string id)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM torrents WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static string[] SplitLines(string s) =>
        s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static HashSet<int> ParseIndexes(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries)
         .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : -1)
         .Where(i => i >= 0)
         .ToHashSet();
}
