namespace Itorrent.Core.Settings;

/// <summary>
/// Configurações do usuário. Persistidas em SQLite pelo SettingsRepository.
/// </summary>
public sealed record AppSettings
{
    public string DefaultSavePath { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Itorrent");

    /// <summary>Porta do BitTorrent (TCP e UDP). Única porta que o app escuta.</summary>
    public int ListenPort { get; init; } = Random.Shared.Next(49152, 65535);

    public bool StopSeedingOnComplete { get; init; } = true;

    public bool TurboMode { get; init; }

    public bool AllowPortForwarding { get; init; } = true;
    public bool AllowDht { get; init; } = true;
    public bool AllowPeerExchange { get; init; } = true;
    public bool AllowLocalPeerDiscovery { get; init; } = true;
    public bool AddPublicTrackers { get; init; } = true;

    public int MaxConnectionsPerTorrent { get; init; } = 100;
    public int TurboConnectionsPerTorrent { get; init; } = 300;

    /// <summary>Limites em KB/s. 0 = sem limite.</summary>
    public int MaxDownloadKBps { get; init; }
    public int MaxUploadKBps { get; init; }

    public bool StartWithWindows { get; init; }
    public bool PreventSleepWhileDownloading { get; init; } = true;
    public bool NotifyOnComplete { get; init; } = true;
    public bool MinimizeToTrayOnClose { get; init; } = true;

    /// <summary>
    /// Encerra o app sozinho depois de ficar este tempo em segundo plano sem baixar nada.
    /// 0 = nunca.
    /// </summary>
    public int AutoExitIdleMinutes { get; init; } = 120;

    /// <summary>Opções oferecidas na tela de configurações (em minutos).</summary>
    public static readonly IReadOnlyList<int> AutoExitChoices = [0, 30, 120, 180];

    public AppSettings Normalize() => this with
    {
        AutoExitIdleMinutes = AutoExitChoices.Contains(AutoExitIdleMinutes) ? AutoExitIdleMinutes : 120,
        ListenPort = Math.Clamp(ListenPort, 1024, 65535),
        MaxConnectionsPerTorrent = Math.Clamp(MaxConnectionsPerTorrent, 10, 1000),
        TurboConnectionsPerTorrent = Math.Clamp(TurboConnectionsPerTorrent, 200, 500),
        MaxDownloadKBps = Math.Max(0, MaxDownloadKBps),
        MaxUploadKBps = Math.Max(0, MaxUploadKBps),
    };
}
