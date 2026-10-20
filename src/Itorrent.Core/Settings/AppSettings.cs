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

    public bool TurboMode { get; init; }

    /// <summary>Ao concluir, continua semeando (até a meta de ratio) em vez de parar.</summary>
    public bool SeedAfterComplete { get; init; } = true;

    /// <summary>Para de semear sozinho quando o ratio (enviado ÷ baixado) chega aqui. 0 = sem limite.</summary>
    public double SeedRatioGoal { get; init; } = 1.0;

    public const double MaxSeedRatioGoal = 100;

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

    /// <summary>"pt-BR" ou "en". Vazio = ainda não escolhido (usa o do instalador ou o do Windows).</summary>
    public string Language { get; init; } = "";

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
        Language = Localization.Strings.Normalize(Language),
        ListenPort = Math.Clamp(ListenPort, 1024, 65535),
        MaxConnectionsPerTorrent = Math.Clamp(MaxConnectionsPerTorrent, 10, 1000),
        TurboConnectionsPerTorrent = Math.Clamp(TurboConnectionsPerTorrent, 200, 500),
        MaxDownloadKBps = Math.Max(0, MaxDownloadKBps),
        MaxUploadKBps = Math.Max(0, MaxUploadKBps),
        SeedRatioGoal = double.IsFinite(SeedRatioGoal) ? Math.Round(Math.Clamp(SeedRatioGoal, 0, MaxSeedRatioGoal), 2) : 1.0,
    };
}
