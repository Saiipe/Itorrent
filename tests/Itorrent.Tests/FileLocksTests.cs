using Itorrent.Core.Engine;

namespace Itorrent.Tests;

public sealed class FileLocksTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "itorrent-locks-" + Guid.NewGuid().ToString("N"));

    private string NewFile()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    [Fact]
    public void Held_file_cannot_be_deleted_or_renamed_but_can_be_read_and_written()
    {
        using var locks = new FileLocks();
        var path = NewFile();
        locks.Hold(path);
        Assert.True(locks.IsHeld(path));

        Assert.ThrowsAny<IOException>(() => File.Delete(path));
        Assert.ThrowsAny<IOException>(() => File.Move(path, path + ".novo"));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
        using (var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            writer.WriteByte(9);
        Assert.True(File.Exists(path));

        locks.Release(path);
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Missing_file_is_ignored_and_dispose_releases_everything()
    {
        var path = NewFile();
        var locks = new FileLocks();
        locks.Hold(Path.Combine(_dir, "nao-existe.bin"));
        locks.Hold(path);
        locks.Dispose();
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Suspended_file_is_not_held_again_until_the_suspension_ends()
    {
        using var locks = new FileLocks();
        var path = NewFile();
        locks.Hold(path);

        using (locks.Suspend(path))
        {
            // A sincronização periódica tenta travar no meio da exclusão: não pode.
            locks.Hold(path);
            Assert.False(locks.IsHeld(path));
            File.Delete(path);
        }

        Assert.False(File.Exists(path));
        File.WriteAllBytes(path, [1]);
        locks.Hold(path);
        Assert.True(locks.IsHeld(path));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }
}
