using System.Net;
using Itorrent.Core.Settings;
using MonoTorrent.Client;
using MonoTorrent.Connections;
using MonoTorrent.PieceWriter;

namespace Itorrent.Core.Engine;

/// <summary>
/// Traduz AppSettings em configurações do MonoTorrent. O "Modo Turbo" vive aqui:
/// liga UPnP/NAT-PMP, DHT, PEX, LPD, trackers públicos e sobe os limites de conexão.
/// </summary>
public static class EngineFactory
{
    /// <summary>Trackers públicos adicionados a torrents não privados (configurável, desligável).</summary>
    public static readonly IReadOnlyList<string> PublicTrackers =
    [
        "udp://tracker.opentrackr.org:1337/announce",
        "udp://open.stealth.si:80/announce",
        "udp://tracker.torrent.eu.org:451/announce",
        "udp://exodus.desync.com:6969/announce",
        "udp://open.demonii.com:1337/announce",
        "udp://tracker.openbittorrent.com:6969/announce",
        "udp://explodie.org:6969/announce",
        "https://tracker.tamersunion.org:443/announce",
    ];

    /// <summary>Valores efetivos: no Turbo, as opções de descoberta são forçadas a ligar.</summary>
    public static EffectiveProfile Profile(AppSettings s) => s.TurboMode
        ? new EffectiveProfile(
            PortForwarding: true, Dht: true, PeerExchange: true, LocalPeerDiscovery: true, PublicTrackers: true,
            ConnectionsPerTorrent: s.TurboConnectionsPerTorrent,
            GlobalConnections: Math.Max(1000, s.TurboConnectionsPerTorrent * 3),
            HalfOpenConnections: 24,
            UploadSlots: 8,
            DiskCacheBytes: 32 * 1024 * 1024)
        : new EffectiveProfile(
            s.AllowPortForwarding, s.AllowDht, s.AllowPeerExchange, s.AllowLocalPeerDiscovery, s.AddPublicTrackers,
            ConnectionsPerTorrent: s.MaxConnectionsPerTorrent,
            GlobalConnections: Math.Max(200, s.MaxConnectionsPerTorrent * 2),
            HalfOpenConnections: 8,
            UploadSlots: 4,
            DiskCacheBytes: 5 * 1024 * 1024);

    public static EngineSettings CreateEngineSettings(AppSettings s, string cacheDirectory)
    {
        var p = Profile(s);
        var b = new EngineSettingsBuilder
        {
            CacheDirectory = cacheDirectory,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            AutoSaveLoadDhtCache = true,
            FastResumeMode = FastResumeMode.BestEffort,

            // Só a porta do BitTorrent, TCP e UDP. Nenhum servidor HTTP/API.
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new(IPAddress.Any, s.ListenPort),
                ["ipv6"] = new(IPAddress.IPv6Any, s.ListenPort),
            },
            DhtEndPoint = p.Dht ? new IPEndPoint(IPAddress.Any, s.ListenPort) : null,
            AllowPortForwarding = p.PortForwarding,
            AllowLocalPeerDiscovery = p.LocalPeerDiscovery,

            // MSE/PE "preferida": tenta criptografado, aceita texto puro.
            AllowedEncryption = [EncryptionType.RC4Full, EncryptionType.RC4Header, EncryptionType.PlainText],

            MaximumConnections = p.GlobalConnections,
            MaximumHalfOpenConnections = p.HalfOpenConnections,
            MaximumDownloadRate = s.MaxDownloadKBps * 1024,
            MaximumUploadRate = s.MaxUploadKBps * 1024,
            DiskCacheBytes = p.DiskCacheBytes,
            DiskCachePolicy = CachePolicy.WritesOnly,
            UsePartialFiles = false,
            // O servidor HTTP de streaming do MonoTorrent só sobe se CreateHttpStreamAsync for
            // chamado — e o Itorrent nunca chama. Não há porta além da do BitTorrent.
        };
        return b.ToSettings();
    }

    public static TorrentSettings CreateTorrentSettings(AppSettings s, bool isPrivate)
    {
        var p = Profile(s);
        return new TorrentSettingsBuilder
        {
            // Torrents privados não podem usar DHT/PEX (regra dos trackers privados).
            AllowDht = p.Dht && !isPrivate,
            AllowPeerExchange = p.PeerExchange && !isPrivate,
            AllowInitialSeeding = false,
            CreateContainingDirectory = true,
            MaximumConnections = p.ConnectionsPerTorrent,
            UploadSlots = p.UploadSlots,
        }.ToSettings();
    }
}

public sealed record EffectiveProfile(
    bool PortForwarding,
    bool Dht,
    bool PeerExchange,
    bool LocalPeerDiscovery,
    bool PublicTrackers,
    int ConnectionsPerTorrent,
    int GlobalConnections,
    int HalfOpenConnections,
    int UploadSlots,
    int DiskCacheBytes);
