using System.Text.Json;
using Itorrent.Core.Settings;

namespace Itorrent.Core.Storage;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler<AppSettings>? Changed;
    void Save(AppSettings settings);
}

/// <summary>
/// Guarda as configurações como JSON numa linha da tabela settings.
/// </summary>
public sealed class SettingsRepository : ISettingsService
{
    private const string Key = "app";
    private readonly Database _db;

    public SettingsRepository(Database db)
    {
        _db = db;
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public void Save(AppSettings settings)
    {
        settings = settings.Normalize();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO settings (key, value) VALUES ($k, $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        cmd.Parameters.AddWithValue("$k", Key);
        cmd.Parameters.AddWithValue("$v", JsonSerializer.Serialize(settings));
        cmd.ExecuteNonQuery();
        Current = settings;
        Changed?.Invoke(this, settings);
    }

    private AppSettings Load()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", Key);
        if (cmd.ExecuteScalar() is string json)
        {
            try
            {
                if (JsonSerializer.Deserialize<AppSettings>(json) is { } loaded)
                    return loaded.Normalize();
            }
            catch (JsonException)
            {
                // Configuração corrompida: volta ao padrão.
            }
        }

        // Primeira execução: grava os padrões (inclui a porta sorteada).
        var defaults = new AppSettings().Normalize();
        Current = defaults;
        Save(defaults);
        return defaults;
    }
}
