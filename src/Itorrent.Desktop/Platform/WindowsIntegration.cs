using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Itorrent.Desktop.Platform;

/// <summary>Iniciar com o Windows (HKCU\...\Run). Nunca HKLM.</summary>
public static class StartupRegistry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Itorrent";

    /// <summary>
    /// Estado real no registro. O instalador também pode ligar a opção, então o registro
    /// (e não só a configuração salva) é a fonte da verdade.
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --tray");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

/// <summary>
/// Registra magnet: e .torrent em HKCU\Software\Classes (sem administrador).
/// O instalador faz o mesmo; isto permite associar também pela tela de opções.
/// </summary>
public static class ProtocolRegistration
{
    private const string ProgId = "Itorrent.Torrent";

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\magnet\shell\open\command");
        return key?.GetValue(null) is string cmd && cmd.Contains(Environment.ProcessPath ?? "?", StringComparison.OrdinalIgnoreCase);
    }

    public static void Register()
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do executável desconhecido");
        var command = $"\"{exe}\" \"%1\"";

        using (var magnet = Registry.CurrentUser.CreateSubKey(@"Software\Classes\magnet"))
        {
            magnet.SetValue(null, "URL:Magnet Protocol");
            magnet.SetValue("URL Protocol", "");
            using var icon = magnet.CreateSubKey("DefaultIcon");
            icon.SetValue(null, $"\"{exe}\",0");
            using var cmd = magnet.CreateSubKey(@"shell\open\command");
            cmd.SetValue(null, command);
        }

        using (var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            prog.SetValue(null, "Arquivo Torrent");
            using var icon = prog.CreateSubKey("DefaultIcon");
            icon.SetValue(null, $"\"{exe}\",0");
            using var cmd = prog.CreateSubKey(@"shell\open\command");
            cmd.SetValue(null, command);
        }

        using (var ext = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.torrent"))
        {
            ext.SetValue(null, ProgId);
            ext.SetValue("Content Type", "application/x-bittorrent");
        }

        NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
}

/// <summary>Impede a suspensão do PC enquanto houver downloads ativos.</summary>
public static class KeepAwake
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;
    private static bool _active;

    public static void Set(bool active)
    {
        if (active == _active)
            return;
        _active = active;
        if (NativeMethods.SetThreadExecutionState(active ? EsContinuous | EsSystemRequired : EsContinuous) == 0)
            Serilog.Log.Warning("SetThreadExecutionState falhou");
    }
}

internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint SetThreadExecutionState(uint esFlags);

    [LibraryImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
