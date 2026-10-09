using System.Security;
using System.Text;
using Itorrent.Core.Security;

namespace Itorrent.Tests;

public class PathGuardTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "itorrent-root");

    [Theory]
    [InlineData("../evil.exe")]
    [InlineData("..\\evil.exe")]
    [InlineData("pasta/../../evil.exe")]
    [InlineData("../../AppData/Roaming/Microsoft/Windows/Start Menu/Programs/Startup/x.exe")]
    [InlineData("C:\\Windows\\System32\\x.dll")]
    [InlineData("C:x.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("\\\\server\\share\\x")]
    [InlineData("CON")]
    [InlineData("pasta/nul.txt")]
    [InlineData("COM1.log")]
    [InlineData("arquivo.txt:Zone.Identifier")]
    [InlineData("")]
    public void SafePath_rejects_malicious_paths(string relative)
    {
        Assert.ThrowsAny<Exception>(() => PathGuard.SafePath(Root, relative));
        Assert.False(PathGuard.IsSafe(Root, relative));
    }

    [Fact]
    public void SafePath_rejects_paths_over_the_length_limit()
    {
        var longName = string.Join('/', Enumerable.Repeat(new string('a', 200), 8));
        Assert.Throws<SecurityException>(() => PathGuard.SafePath(Root, longName));
    }

    [Theory]
    [InlineData("filme.mp4")]
    [InlineData("Ubuntu/ubuntu-24.04.iso")]
    [InlineData("pasta\\sub\\arquivo.txt")]
    [InlineData("..arquivo com pontos..txt")]
    [InlineData("console.txt")]
    public void SafePath_accepts_normal_paths(string relative)
    {
        var full = PathGuard.SafePath(Root, relative);
        Assert.StartsWith(Path.GetFullPath(Root), full, StringComparison.OrdinalIgnoreCase);
    }
}

public class LinkValidatorTests
{
    private const string Hash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c";

    [Theory]
    [InlineData("magnet:?xt=urn:btih:" + Hash)]
    [InlineData("magnet:?xt=urn:btih:" + Hash + "&dn=Big+Buck+Bunny&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337")]
    [InlineData("MAGNET:?xt=urn:btih:" + Hash)]
    public void Valid_magnets_are_accepted(string link) => Assert.True(LinkValidator.IsValidMagnet(link));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://example.com/x.torrent")]
    [InlineData("magnet:")]
    [InlineData("magnet:?dn=sem-hash")]
    [InlineData("magnet:?xt=urn:btih:nao-e-hash")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    public void Invalid_magnets_are_rejected(string? link) => Assert.False(LinkValidator.IsValidMagnet(link));

    [Fact]
    public void Giant_magnet_is_rejected()
    {
        var link = "magnet:?xt=urn:btih:" + Hash + "&dn=" + new string('a', LinkValidator.MaxMagnetLength);
        Assert.False(LinkValidator.IsValidMagnet(link));
    }

    [Fact]
    public void File_trackers_are_discarded()
    {
        var link = "magnet:?xt=urn:btih:" + Hash
                   + "&tr=file%3A%2F%2F%2FC%3A%2Fx&tr=udp%3A%2F%2Fok.example%3A80&tr=ftp%3A%2F%2Fx.example";
        Assert.True(LinkValidator.TryParseMagnet(link, out var magnet));
        Assert.Equal(["udp://ok.example:80"], magnet.AnnounceUrls);
    }

    [Theory]
    [InlineData("udp://tracker.example:1337/announce", true)]
    [InlineData("http://tracker.example/announce", true)]
    [InlineData("https://tracker.example/announce", true)]
    [InlineData("file:///C:/x", false)]
    [InlineData("ws://tracker.example", false)]
    [InlineData("nao e url", false)]
    public void Tracker_schemes_are_whitelisted(string url, bool allowed) =>
        Assert.Equal(allowed, LinkValidator.IsAllowedTracker(url));

    [Fact]
    public void Torrent_over_10MB_is_rejected()
    {
        var data = new byte[LinkValidator.MaxTorrentFileBytes + 1];
        Assert.False(LinkValidator.TryLoadTorrent(data, out _));
    }

    [Fact]
    public void Valid_torrent_is_loaded()
    {
        Assert.True(LinkValidator.TryLoadTorrent(TestTorrents.SingleFile("ubuntu.iso"), out var t));
        Assert.Equal("ubuntu.iso", t.Name);
    }

    [Fact]
    public void Fuzzed_torrents_never_throw()
    {
        var valid = TestTorrents.SingleFile("arquivo.bin");
        var rnd = new Random(1234);
        for (var i = 0; i < 3000; i++)
        {
            byte[] data;
            if (i % 3 == 0)
            {
                data = new byte[rnd.Next(0, 512)];
                rnd.NextBytes(data);
            }
            else
            {
                data = (byte[])valid.Clone();
                for (var j = rnd.Next(1, 8); j > 0; j--)
                    data[rnd.Next(data.Length)] = (byte)rnd.Next(256);
                if (i % 5 == 0)
                    data = data[..rnd.Next(data.Length)];
            }

            var ex = Record.Exception(() => LinkValidator.TryLoadTorrent(data, out _));
            Assert.Null(ex);
        }
    }
}

public class FileRiskCheckerTests
{
    [Theory]
    [InlineData("filme.mp4.exe", FileRisk.DoubleExtension)]
    [InlineData("pasta/foto.jpg.scr", FileRisk.DoubleExtension)]
    [InlineData("setup.exe", FileRisk.Executable)]
    [InlineData("atalho.lnk", FileRisk.Executable)]
    [InlineData("script.PS1", FileRisk.Executable)]
    [InlineData("instalar.bat", FileRisk.Executable)]
    [InlineData("x.vbs", FileRisk.Executable)]
    [InlineData("filme.mp4", FileRisk.None)]
    [InlineData("ubuntu.iso", FileRisk.None)]
    [InlineData("leia-me.txt", FileRisk.None)]
    public void Detects_dangerous_extensions(string path, FileRisk expected) =>
        Assert.Equal(expected, FileRiskChecker.GetRisk(path));

    [Fact]
    public void Writes_mark_of_the_web()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var path = Path.Combine(Path.GetTempPath(), $"itorrent-motw-{Guid.NewGuid():N}.exe");
        File.WriteAllText(path, "x");
        try
        {
            Assert.True(FileRiskChecker.WriteMarkOfTheWeb(path));
            var zone = File.ReadAllText(path + ":Zone.Identifier");
            Assert.Contains("ZoneId=3", zone, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

internal static class TestTorrents
{
    /// <summary>Monta um .torrent mínimo em bencode.</summary>
    public static byte[] SingleFile(string name, long length = 10)
    {
        const string announce = "udp://tracker.example:1337/ann";
        var head = Encoding.ASCII.GetBytes(
            $"d8:announce{announce.Length}:{announce}4:infod6:lengthi{length}e4:name{name.Length}:{name}" +
            "12:piece lengthi16384e6:pieces20:");
        return [.. head, .. new byte[20], .. "ee"u8.ToArray()];
    }
}
