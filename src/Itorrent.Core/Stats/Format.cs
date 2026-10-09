using System.Globalization;

namespace Itorrent.Core.Stats;

public static class Format
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long bytes)
    {
        if (bytes < 0)
            return "?";
        double v = bytes;
        var u = 0;
        while (v >= 1024 && u < Units.Length - 1)
        {
            v /= 1024;
            u++;
        }
        return u == 0
            ? string.Create(PtBr, $"{bytes} B")
            : string.Create(PtBr, $"{v:0.##} {Units[u]}");
    }

    public static string Rate(long bytesPerSecond) =>
        bytesPerSecond <= 0 ? "0 KB/s" : Bytes(bytesPerSecond) + "/s";

    public static string Eta(TimeSpan? eta)
    {
        if (eta is not { } t || t.TotalDays > 365)
            return "∞";
        if (t.TotalDays >= 1)
            return string.Create(PtBr, $"{(int)t.TotalDays}d {t.Hours}h");
        if (t.TotalHours >= 1)
            return string.Create(PtBr, $"{(int)t.TotalHours}h {t.Minutes:00}m");
        return string.Create(PtBr, $"{t.Minutes}m {t.Seconds:00}s");
    }

    /// <summary>Formato do qBittorrent: conectados (total na rede).</summary>
    public static string Peers(int connected, int total) =>
        total >= 0 ? string.Create(PtBr, $"{connected} ({total})") : string.Create(PtBr, $"{connected} (?)");
}
