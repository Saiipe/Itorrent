using MonoTorrent;

namespace Itorrent.Core.Security;

/// <summary>
/// Porta de entrada de todo link ou arquivo externo (navegador, pipe, .torrent).
/// Nada chega ao TorrentService sem passar por aqui.
/// </summary>
public static class LinkValidator
{
    public const int MaxMagnetLength = 8192;
    public const long MaxTorrentFileBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedTrackerSchemes = ["http", "https", "udp"];

    public static bool IsValidMagnet(string? s) => TryParseMagnet(s, out _);

    /// <summary>
    /// Valida o magnet e devolve uma cópia sem trackers de esquema não permitido.
    /// </summary>
    public static bool TryParseMagnet(string? s, out MagnetLink magnet)
    {
        magnet = null!;
        if (string.IsNullOrWhiteSpace(s))
            return false;

        s = s.Trim();
        if (s.Length > MaxMagnetLength || !s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            return false;

        if (s.Any(char.IsControl))
            return false;

        try
        {
            var parsed = MagnetLink.Parse(s);
            magnet = new MagnetLink(
                parsed.InfoHashes,
                SanitizeName(parsed.Name),
                FilterTrackers(parsed.AnnounceUrls).ToList(),
                parsed.Webseeds.Where(IsAllowedWebSeed),
                parsed.Size);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Carrega um .torrent com limite de tamanho e parse protegido.
    /// </summary>
    public static bool TryLoadTorrent(ReadOnlySpan<byte> data, out Torrent torrent)
    {
        torrent = null!;
        if (data.IsEmpty || data.Length > MaxTorrentFileBytes)
            return false;

        try
        {
            return Torrent.TryLoad(data, out torrent!) && torrent is not null && torrent.Files.Count > 0;
        }
        catch
        {
            torrent = null!;
            return false;
        }
    }

    public static bool TryLoadTorrentFile(string path, out Torrent torrent, out byte[] data)
    {
        torrent = null!;
        data = [];
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > MaxTorrentFileBytes)
                return false;

            data = File.ReadAllBytes(path);
            return TryLoadTorrent(data, out torrent);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsAllowedTracker(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && AllowedTrackerSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(uri.Host);

    public static IEnumerable<string> FilterTrackers(IEnumerable<string>? urls) =>
        (urls ?? []).Where(IsAllowedTracker).Distinct(StringComparer.OrdinalIgnoreCase);

    private static bool IsAllowedWebSeed(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? SanitizeName(string? name)
    {
        if (name is null)
            return null;
        var clean = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length > 255 ? clean[..255] : clean;
    }
}
