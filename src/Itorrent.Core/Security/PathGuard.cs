using Itorrent.Core.Localization;
using System.Security;

namespace Itorrent.Core.Security;

/// <summary>
/// Garante que todo arquivo do torrent fique dentro da pasta de download
/// (contra path traversal, caminhos absolutos, nomes reservados e ADS).
/// </summary>
public static class PathGuard
{
    public const int MaxPathLength = 260 * 4;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string SafePath(string root, string relative)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(relative);

        if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Contains(':', StringComparison.Ordinal))
            throw new SecurityException(Strings.T("Path.Absolute"));

        foreach (var segment in relative.Split('/', '\\'))
        {
            if (segment.Length == 0)
                continue;
            if (segment is "." or "..")
                throw new SecurityException(Strings.T("Path.DotDot"));
            var stem = segment.Split('.')[0].TrimEnd(' ');
            if (ReservedNames.Contains(stem))
                throw new SecurityException(Strings.T("Path.Reserved", segment));
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment.Any(char.IsControl))
                throw new SecurityException(Strings.T("Path.InvalidChars"));
        }

        var baseDir = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(baseDir, relative));
        if (!full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            throw new SecurityException(Strings.T("Path.Outside"));
        if (full.Length > MaxPathLength)
            throw new SecurityException(Strings.T("Path.TooLong"));
        return full;
    }

    public static bool IsSafe(string root, string relative)
    {
        try
        {
            SafePath(root, relative);
            return true;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Valida a pasta escolhida pelo usuário como destino.</summary>
    public static bool IsValidSaveDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return false;
        try
        {
            var full = Path.GetFullPath(path);
            return full.Length < MaxPathLength;
        }
        catch
        {
            return false;
        }
    }
}
