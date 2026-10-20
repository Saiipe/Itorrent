using Itorrent.Core.Engine;
using Itorrent.Core.Policies;
using Itorrent.Core.Settings;
using Itorrent.Core.Stats;
using Itorrent.Core.Storage;
using MonoTorrent.Client;

namespace Itorrent.Tests;

public class PolicyEngineTests
{
    private sealed class FakeTorrent : ITorrentControl
    {
        public int Stops { get; private set; }

        public Task StopAsync()
        {
            Stops++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Torrent_reaching_seeding_is_stopped()
    {
        var t = new FakeTorrent();
        Assert.True(PolicyEngine.IsCompletion(TorrentState.Downloading, TorrentState.Seeding));
        await PolicyEngine.OnCompletedAsync(t);
        Assert.Equal(1, t.Stops);
    }

    [Theory]
    [InlineData(TorrentState.Seeding, TorrentState.Seeding, false)]
    [InlineData(TorrentState.Downloading, TorrentState.Paused, false)]
    [InlineData(TorrentState.Hashing, TorrentState.Seeding, true)]
    public void Detects_completion_transitions(TorrentState from, TorrentState to, bool expected) =>
        Assert.Equal(expected, PolicyEngine.IsCompletion(from, to));

    [Theory]
    [InlineData(TorrentStatus.Downloading, true)]
    [InlineData(TorrentStatus.Stalled, true)]
    [InlineData(TorrentStatus.FetchingMetadata, true)]
    [InlineData(TorrentStatus.Checking, true)]
    [InlineData(TorrentStatus.Seeding, true)]
    [InlineData(TorrentStatus.Paused, false)]
    [InlineData(TorrentStatus.Completed, false)]
    [InlineData(TorrentStatus.Error, false)]
    public void Only_active_transfers_keep_the_app_busy(TorrentStatus status, bool busy) =>
        Assert.Equal(busy, PolicyEngine.KeepsAppBusy(status));

    [Theory]
    [InlineData(119, 120, false)]
    [InlineData(120, 120, true)]
    [InlineData(31, 30, true)]
    [InlineData(179, 180, false)]
    [InlineData(10_000, 0, false)]
    public void Auto_exit_after_idle_time(int idleFor, int setting, bool expected)
    {
        var now = DateTimeOffset.Now;
        Assert.Equal(expected, PolicyEngine.ShouldAutoExit(now.AddMinutes(-idleFor), now, setting));
    }

    [Fact]
    public void Auto_exit_defaults_to_two_hours_and_rejects_odd_values()
    {
        Assert.Equal(120, new AppSettings().AutoExitIdleMinutes);
        Assert.Equal(120, new AppSettings { AutoExitIdleMinutes = 45 }.Normalize().AutoExitIdleMinutes);
        Assert.Equal(0, new AppSettings { AutoExitIdleMinutes = 0 }.Normalize().AutoExitIdleMinutes);
        Assert.True(new AppSettings().MinimizeToTrayOnClose);
    }

    [Theory]
    [InlineData(500, 1000, 1000, 0.5)]
    [InlineData(1000, 1000, 1000, 1.0)]
    // Baixado por versão sem contador: a base é o que está no disco.
    [InlineData(500, 0, 1000, 0.5)]
    // Baixou mais do que tem (peças refeitas): a base é o baixado.
    [InlineData(1000, 2000, 1000, 0.5)]
    [InlineData(0, 0, 0, 0.0)]
    [InlineData(100, 0, 0, 0.0)]
    public void Share_ratio_is_uploaded_over_downloaded(long up, long down, long have, double expected) =>
        Assert.Equal(expected, PolicyEngine.ShareRatio(up, down, have), 6);

    [Fact]
    public void Ratio_goal_and_bytes_left()
    {
        Assert.False(PolicyEngine.ReachedRatioGoal(0.99, 1.0));
        Assert.True(PolicyEngine.ReachedRatioGoal(1.0, 1.0));
        Assert.False(PolicyEngine.ReachedRatioGoal(50, 0));
        Assert.Equal(400, PolicyEngine.BytesToGoal(600, 1000, 1000, 1.0));
        Assert.Equal(1400, PolicyEngine.BytesToGoal(600, 1000, 1000, 2.0));
        Assert.Equal(0, PolicyEngine.BytesToGoal(1200, 1000, 1000, 1.0));
        Assert.Equal(0, PolicyEngine.BytesToGoal(0, 1000, 1000, 0));
    }

    [Fact]
    public void Seeds_after_completion_by_default_up_to_ratio_one()
    {
        var s = new AppSettings();
        Assert.True(s.SeedAfterComplete);
        Assert.Equal(1.0, s.SeedRatioGoal);
        Assert.Equal(0, new AppSettings { SeedRatioGoal = -3 }.Normalize().SeedRatioGoal);
        Assert.Equal(AppSettings.MaxSeedRatioGoal, new AppSettings { SeedRatioGoal = 1e9 }.Normalize().SeedRatioGoal);
        Assert.Equal(1.0, new AppSettings { SeedRatioGoal = double.NaN }.Normalize().SeedRatioGoal);
    }

    [Fact]
    public void Partial_selection_completes_at_100_percent()
    {
        Assert.True(PolicyEngine.IsPartialCompletion(TorrentState.Downloading, 100));
        Assert.False(PolicyEngine.IsPartialCompletion(TorrentState.Downloading, 99.5));
        Assert.False(PolicyEngine.IsPartialCompletion(TorrentState.Stopped, 100));
    }
}

public class TurboModeTests
{
    [Fact]
    public void Turbo_forces_discovery_and_raises_connection_limits()
    {
        var off = new AppSettings { TurboMode = false, AllowDht = false, AllowPortForwarding = false, AddPublicTrackers = false };
        var on = off with { TurboMode = true };

        var p = EngineFactory.Profile(on);
        Assert.True(p.Dht && p.PortForwarding && p.PeerExchange && p.LocalPeerDiscovery && p.PublicTrackers);
        Assert.InRange(p.ConnectionsPerTorrent, 200, 500);
        Assert.True(p.GlobalConnections > EngineFactory.Profile(off).GlobalConnections);
        Assert.False(EngineFactory.Profile(off).Dht);
    }

    [Fact]
    public void Private_torrents_never_use_dht_or_pex()
    {
        var s = EngineFactory.CreateTorrentSettings(new AppSettings { TurboMode = true }, isPrivate: true);
        Assert.False(s.AllowDht);
        Assert.False(s.AllowPeerExchange);
    }

    [Fact]
    public void Engine_listens_only_on_the_bittorrent_port()
    {
        var s = EngineFactory.CreateEngineSettings(new AppSettings { ListenPort = 51413 }, Path.GetTempPath());
        Assert.All(s.ListenEndPoints.Values, ep => Assert.Equal(51413, ep.Port));
        Assert.Equal(51413, s.DhtEndPoint!.Port);
    }
}

public sealed class PersistenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "itorrent-tests-" + Guid.NewGuid().ToString("N"));

    private string DbPath => Path.Combine(_dir, "itorrent.db");

    [Fact]
    public void Settings_survive_reopen()
    {
        var repo = new SettingsRepository(new Database(DbPath));
        repo.Save(repo.Current with { TurboMode = true, ListenPort = 50000, DefaultSavePath = "D:\\Torrents" });

        var reopened = new SettingsRepository(new Database(DbPath));
        Assert.True(reopened.Current.TurboMode);
        Assert.Equal(50000, reopened.Current.ListenPort);
        Assert.Equal("D:\\Torrents", reopened.Current.DefaultSavePath);
    }

    [Fact]
    public void Torrent_list_survives_reopen_and_handles_hostile_names()
    {
        var repo = new TorrentRepository(new Database(DbPath));
        const string hostile = "x'); DROP TABLE torrents; --";
        repo.Upsert(new TorrentRecord("ABC", hostile, "C:\\dl", [1, 2, 3], ["udp://t.example:1"],
            new HashSet<int> { 2, 5 }, StoredStatus.Paused, 100, 60, DateTimeOffset.Now, null));
        repo.SetStatus("ABC", StoredStatus.Completed, DateTimeOffset.Now);

        var all = new TorrentRepository(new Database(DbPath)).GetAll();
        var r = Assert.Single(all);
        Assert.Equal(hostile, r.Name);
        Assert.Equal(StoredStatus.Completed, r.Status);
        Assert.NotNull(r.CompletedAt);
        Assert.Equal([2, 5], r.SkippedFiles.Order());
        Assert.Equal([1, 2, 3], r.Metadata);

        repo.Delete("ABC");
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Seeding_status_and_ratio_counter_survive_reopen()
    {
        var repo = new TorrentRepository(new Database(DbPath));
        repo.Upsert(new TorrentRecord("ABC", "x", "C:\\dl", [1], [], new HashSet<int>(), StoredStatus.Completed,
            100, 100, DateTimeOffset.Now, DateTimeOffset.Now, UploadedBytes: 10, DownloadedBytes: 100));
        repo.SetStatus("ABC", StoredStatus.Seeding);
        repo.SetTransferred("ABC", 250, 120);

        var r = Assert.Single(new TorrentRepository(new Database(DbPath)).GetAll());
        Assert.Equal(StoredStatus.Seeding, r.Status);
        Assert.Equal(250, r.UploadedBytes);
        Assert.Equal(120, r.DownloadedBytes);
    }

    [Fact]
    public void Old_database_gets_the_ratio_columns()
    {
        Directory.CreateDirectory(_dir);
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE torrents (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL, save_path TEXT NOT NULL, metadata BLOB NOT NULL,
                    extra_trackers TEXT NOT NULL DEFAULT '', skipped_files TEXT NOT NULL DEFAULT '',
                    status INTEGER NOT NULL, total_size INTEGER NOT NULL, selected_size INTEGER NOT NULL,
                    added_at TEXT NOT NULL, completed_at TEXT NULL);
                INSERT INTO torrents VALUES ('OLD', 'velho', 'C:\dl', x'01', '', '', 2, 10, 10, '2026-01-01T00:00:00.0000000-03:00', NULL);
                """;
            cmd.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var r = Assert.Single(new TorrentRepository(new Database(DbPath)).GetAll());
        Assert.Equal("velho", r.Name);
        Assert.Equal(0, r.UploadedBytes);
        Assert.Equal(0, r.DownloadedBytes);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }
}
