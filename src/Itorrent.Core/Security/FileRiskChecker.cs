namespace Itorrent.Core.Security;

/// <summary>
/// Detecta executáveis disfarçados e grava o Mark of the Web nos arquivos baixados.
/// O app nunca abre nem executa arquivos baixados.
/// </summary>
public static class FileRiskChecker
{
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".scr", ".lnk", ".js", ".vbs",
        ".com", ".pif", ".jse", ".vbe", ".wsf", ".wsh", ".hta", ".cpl", ".msc", ".jar", ".dll", ".reg",
    };

    private static readonly HashSet<string> DecoyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".mp3", ".flac", ".wav", ".jpg", ".jpeg", ".png", ".gif",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip", ".rar", ".7z", ".iso", ".srt",
    };

    public static bool IsDangerous(string path) => GetRisk(path) != FileRisk.None;

    public static FileRisk GetRisk(string path)
    {
        var name = Path.GetFileName(path.Replace('\\', '/').TrimEnd(' ', '.'));
        var ext = Path.GetExtension(name);
        if (!DangerousExtensions.Contains(ext))
            return FileRisk.None;

        // filme.mp4.exe → extensão dupla
        var inner = Path.GetExtension(Path.GetFileNameWithoutExtension(name));
        return DecoyExtensions.Contains(inner) ? FileRisk.DoubleExtension : FileRisk.Executable;
    }

    /// <summary>
    /// Marca o arquivo como vindo da internet (ZoneId=3) para SmartScreen e Office.
    /// Só tem efeito em NTFS; em outros sistemas é ignorado.
    /// </summary>
    public static bool WriteMarkOfTheWeb(string path)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
            return false;
        try
        {
            File.WriteAllText(path + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public enum FileRisk
{
    None,
    Executable,
    DoubleExtension,
}
