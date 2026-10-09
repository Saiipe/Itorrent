using Microsoft.VisualBasic.FileIO;

namespace Itorrent.Desktop.Platform;

/// <summary>Apaga mandando para a Lixeira do Windows (dá para recuperar).</summary>
public static class RecycleBin
{
    public static void Delete(string path) =>
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
}
