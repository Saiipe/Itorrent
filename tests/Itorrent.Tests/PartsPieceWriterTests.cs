using Itorrent.Core.Engine;
using MonoTorrent;
using MonoTorrent.PieceWriter;

namespace Itorrent.Tests;

public sealed class PartsPieceWriterTests : IDisposable
{
    private sealed class FakeFile(string fullPath) : ITorrentManagerFile
    {
        public string Path => System.IO.Path.GetFileName(fullPath);
        public int StartPieceIndex => 0;
        public int EndPieceIndex => 0;
        public int PieceCount => 1;
        public long Length => 64;
        public long Padding => 0;
        public long OffsetInTorrent => 0;
        public MerkleRoot PiecesRoot => default;
        public ReadOnlyBitField BitField => new BitField(1);
        public string FullPath => fullPath;
        public string DownloadCompleteFullPath => fullPath;
        public string DownloadIncompleteFullPath => fullPath;
        public Priority Priority { get; set; } = Priority.Normal;
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "itorrent-parts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Skipped_file_data_goes_to_cache_and_comes_back_when_selected()
    {
        var store = new PartsStore(Path.Combine(_root, "parts"));
        using var writer = new PartsPieceWriter(new DiskWriter(), store);
        var file = new FakeFile(Path.Combine(_root, "download", "sample.txt")) { Priority = Priority.DoNotDownload };

        await writer.WriteAsync(file, 0, new byte[] { 1, 2, 3 });
        await writer.CloseAsync(file);

        Assert.False(File.Exists(file.FullPath));
        Assert.True(store.IsParked(file));

        file.Priority = Priority.Normal;
        var buffer = new byte[3];
        Assert.Equal(3, await writer.ReadAsync(file, 0, buffer));
        await writer.CloseAsync(file);

        Assert.Equal(new byte[] { 1, 2, 3 }, buffer);
        Assert.True(File.Exists(file.FullPath));
        Assert.False(store.IsParked(file));
    }

    [Fact]
    public async Task Selected_file_is_written_in_place()
    {
        var store = new PartsStore(Path.Combine(_root, "parts"));
        using var writer = new PartsPieceWriter(new DiskWriter(), store);
        var file = new FakeFile(Path.Combine(_root, "download", "movie.mkv"));

        await writer.WriteAsync(file, 0, new byte[] { 9 });
        await writer.CloseAsync(file);

        Assert.True(File.Exists(file.FullPath));
        Assert.False(store.IsParked(file));
    }

    [Fact]
    public async Task Removing_torrent_deletes_parked_data()
    {
        var store = new PartsStore(Path.Combine(_root, "parts"));
        using var writer = new PartsPieceWriter(new DiskWriter(), store);
        var file = new FakeFile(Path.Combine(_root, "download", "extra.nfo")) { Priority = Priority.DoNotDownload };

        await writer.WriteAsync(file, 0, new byte[] { 7 });
        await writer.CloseAsync(file);
        store.Delete([file]);

        Assert.False(store.IsParked(file));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "parts")));
    }
}
