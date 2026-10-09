using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Itorrent.Desktop.Assets;

/// <summary>
/// Ícones 16x16 em pixel-art, paleta VGA de 16 cores, desenhados como texto.
/// Exibidos com BitmapScalingMode.NearestNeighbor para manter o serrilhado da época.
/// </summary>
public static class PixelIcons
{
    private static readonly Dictionary<char, uint> Palette = new()
    {
        ['K'] = 0xFF000000, ['W'] = 0xFFFFFFFF, ['L'] = 0xFFC0C0C0, ['D'] = 0xFF808080,
        ['R'] = 0xFFFF0000, ['r'] = 0xFF800000, ['G'] = 0xFF00FF00, ['g'] = 0xFF008000,
        ['B'] = 0xFF0000FF, ['b'] = 0xFF000080, ['Y'] = 0xFFFFFF00, ['y'] = 0xFF808000,
        ['C'] = 0xFF00FFFF, ['c'] = 0xFF008080, ['M'] = 0xFFFF00FF, ['m'] = 0xFF800080,
    };

    public static readonly IReadOnlyDictionary<string, string[]> Maps = new Dictionary<string, string[]>
    {
        ["Magnet"] =
        [
            "................",
            ".KKKKK....KKKKK.",
            ".KLWLK....KLWLK.",
            ".KLLDK....KLLDK.",
            ".KKKKK....KKKKK.",
            ".KRWRK....KRRrK.",
            ".KRRrK....KRRrK.",
            ".KRRrK....KRRrK.",
            ".KRRrK....KRRrK.",
            ".KRRRrK..KRRRrK.",
            "..KRRRrKKRRRrK..",
            "..KRRRRRRRRRrK..",
            "...KrRRRRRRrK...",
            "....KKrrrrKK....",
            "......KKKK......",
            "................",
        ],
        ["TorrentFile"] =
        [
            "..KKKKKKKKK.....",
            "..KWWWWWWWKK....",
            "..KWWWWWWWKWK...",
            "..KWWWWWWWKKKK..",
            "..KWWWWWWWWWWK..",
            "..KWWWWKKKWWWK..",
            "..KWWWWKGKWWWK..",
            "..KWWWWKGKWWWK..",
            "..KWWKKKGKKKWK..",
            "..KWWWKGGGKWWK..",
            "..KWWWWKGKWWWK..",
            "..KWWWWWKWWWWK..",
            "..KWWWWWWWWWWK..",
            "..KWWWWWWWWWWK..",
            "..KKKKKKKKKKKK..",
            "................",
        ],
        ["Pause"] =
        [
            "................",
            "................",
            "...KKKK..KKKK...",
            "...KBBK..KBBK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KBbK..KBbK...",
            "...KKKK..KKKK...",
            "................",
            "................",
        ],
        ["Play"] =
        [
            "................",
            "...KK...........",
            "...KGK..........",
            "...KGGK.........",
            "...KGGGK........",
            "...KGGGGK.......",
            "...KGGGGGK......",
            "...KGGGGGGK.....",
            "...KGGGGGgK.....",
            "...KGGGGgK......",
            "...KGGGgK.......",
            "...KGGgK........",
            "...KGgK.........",
            "...KgK..........",
            "...KK...........",
            "................",
        ],
        ["Remove"] =
        [
            "................",
            "KKK.........KKK.",
            "KRRK.......KRRK.",
            ".KRRK.....KRRK..",
            "..KRRK...KRRK...",
            "...KRRK.KRRK....",
            "....KRRKRRK.....",
            ".....KRRRK......",
            "....KRRKRRK.....",
            "...KRRK.KRRK....",
            "..KRRK...KRRK...",
            ".KRRK.....KRRK..",
            "KRRK.......KRRK.",
            "KKK.........KKK.",
            "................",
            "................",
        ],
        ["Folder"] =
        [
            "................",
            "................",
            "................",
            ".KKKKK..........",
            "KYYYYYK.........",
            "KWWWWWWKKKKKKKK.",
            "KWYYYYYYYYYYYYKD",
            "KWYYYYYYYYYYYYKD",
            "KWYYYYYYYYYYYYKD",
            "KWYYYYYYYYYYYYKD",
            "KWYYYYYYYYYYYYKD",
            "KWYYYYYYYYYYYYKD",
            "KWyyyyyyyyyyyyKD",
            "KKKKKKKKKKKKKKKD",
            ".DDDDDDDDDDDDDDD",
            "................",
        ],
        ["Turbo"] =
        [
            "........KKKKKK..",
            ".......KYYYYK...",
            "......KYYYYK....",
            ".....KYYYYK.....",
            "....KYYYYK......",
            "...KYYYYKKKKK...",
            "..KYYYYYYYYK....",
            "..KKKKKYYYK.....",
            "......KYYK......",
            ".....KYYK.......",
            "....KYYK........",
            "...KYYK.........",
            "..KYYK..........",
            ".KYYK...........",
            ".KYK............",
            ".KK.............",
        ],
        ["Settings"] =
        [
            "................",
            "......KKK.......",
            "..KK..KLK..KK...",
            "..KLKKKLKKKLK...",
            "...KLLLLLLLK....",
            "..KKLLDDDLLKK...",
            "KKKLLD...DLLKKK.",
            "KLLLLD...DLLLLK.",
            "KKKLLD...DLLKKK.",
            "..KKLLDDDLLKK...",
            "...KLLLLLLLK....",
            "..KLKKKLKKKLK...",
            "..KK..KLK..KK...",
            "......KKK.......",
            "................",
            "................",
        ],
        ["File"] =
        [
            "................",
            "..KKKKKKKK......",
            "..KWWWWWWKK.....",
            "..KWWWWWWKWK....",
            "..KWDDDDWKKKK...",
            "..KWWWWWWWWWK...",
            "..KWDDDDDDDWK...",
            "..KWWWWWWWWWK...",
            "..KWDDDDDDDWK...",
            "..KWWWWWWWWWK...",
            "..KWDDDDDDDWK...",
            "..KWWWWWWWWWK...",
            "..KWWWWWWWWWK...",
            "..KKKKKKKKKKK...",
            "................",
            "................",
        ],
        ["Warning"] =
        [
            "................",
            ".......KK.......",
            "......KYYK......",
            "......KYYK......",
            ".....KYYYYK.....",
            ".....KYKKYK.....",
            "....KYYKKYYK....",
            "....KYYKKYYK....",
            "...KYYYKKYYYK...",
            "...KYYYKKYYYK...",
            "..KYYYYYYYYYYK..",
            "..KYYYYKKYYYYK..",
            ".KYYYYYKKYYYYYK.",
            ".KYYYYYYYYYYYYK.",
            "KKKKKKKKKKKKKKKK",
            "................",
        ],
        ["Check"] =
        [
            "................",
            "................",
            "................",
            "............KK..",
            "...........KGK..",
            "..........KGgK..",
            ".........KGgK...",
            "..KK....KGgK....",
            "..KGK..KGgK.....",
            "..KgGK.KGgK.....",
            "...KgGKGgK......",
            "....KgGGK.......",
            ".....KgK........",
            "......K.........",
            "................",
            "................",
        ],
        ["Down"] =
        [
            "................",
            "................",
            ".....KKKKK......",
            ".....KGGgK......",
            ".....KGGgK......",
            ".....KGGgK......",
            "..KKKKGGgKKKK...",
            "...KGGGGGGgK....",
            "....KGGGGgK.....",
            ".....KGGgK......",
            "......KgK.......",
            ".......K........",
            "................",
            "................",
            "................",
            "................",
        ],
        ["Up"] =
        [
            "................",
            "................",
            ".......K........",
            "......KBK.......",
            ".....KBBbK......",
            "....KBBBBbK.....",
            "...KBBBBBBbK....",
            "..KKKKBBbKKKK...",
            ".....KBBbK......",
            ".....KBBbK......",
            ".....KBBbK......",
            ".....KKKKK......",
            "................",
            "................",
            "................",
            "................",
        ],
        ["Hourglass"] =
        [
            "................",
            "..KKKKKKKKKKK...",
            "...KWWWWWWWK....",
            "...KWYYYYYWK....",
            "....KWYYYWK.....",
            ".....KWYWK......",
            "......KYK.......",
            "......KYK.......",
            ".....KWWWK......",
            "....KWWYWWK.....",
            "...KWWYYYWWK....",
            "...KWYYYYYWK....",
            "..KKKKKKKKKKK...",
            "................",
            "................",
            "................",
        ],
        ["Group"] =
        [
            "................",
            "................",
            "KKKKKKKKKKKKKKKK",
            "KbbbbbbbbbbbbbbK",
            "KKKKKKKKKKKKKKKK",
            "KWWWWWWWWWWWWWWK",
            "KWRRWWGGWWBBWWWK",
            "KWRRWWGGWWBBWWWK",
            "KWWWWWWWWWWWWWWK",
            "KWYYWWCCWWMMWWWK",
            "KWYYWWCCWWMMWWWK",
            "KWWWWWWWWWWWWWWK",
            "KKKKKKKKKKKKKKKK",
            "................",
            "................",
            "................",
        ],
        ["Info"] =
        [
            "................",
            ".....KKKKKK.....",
            "...KKBBBBBBKK...",
            "..KBBBBWWBBBBK..",
            "..KBBBBWWBBBBK..",
            ".KBBBBBBBBBBBBK.",
            ".KBBBBWWWBBBBBK.",
            ".KBBBBBWWBBBBBK.",
            ".KBBBBBWWBBBBBK.",
            ".KBBBBBWWBBBBBK.",
            "..KBBBWWWWBBBK..",
            "..KBBBBBBBBBBK..",
            "...KKBBBBBBKK...",
            ".....KKKKKK.....",
            "................",
            "................",
        ],
    };

    private static readonly Dictionary<(string, int), BitmapSource> Cache = [];

    public static BitmapSource Get(string name, int scale = 1)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue((name, scale), out var cached))
                return cached;
            var bmp = Render(Maps.TryGetValue(name, out var map) ? map : Maps["File"], scale);
            Cache[(name, scale)] = bmp;
            return bmp;
        }
    }

    private static BitmapSource Render(string[] map, int scale)
    {
        const int size = 16;
        var px = size * scale;
        var pixels = new uint[px * px];
        for (var y = 0; y < size && y < map.Length; y++)
        {
            for (var x = 0; x < size && x < map[y].Length; x++)
            {
                if (!Palette.TryGetValue(map[y][x], out var color))
                    continue;
                for (var dy = 0; dy < scale; dy++)
                    for (var dx = 0; dx < scale; dx++)
                        pixels[(y * scale + dy) * px + x * scale + dx] = color;
            }
        }
        var bmp = BitmapSource.Create(px, px, 96, 96, PixelFormats.Bgra32, null, pixels, px * 4);
        bmp.Freeze();
        return bmp;
    }
}

/// <summary>Uso em XAML: Source="{a:Pixel Magnet}".</summary>
[MarkupExtensionReturnType(typeof(ImageSource))]
public sealed class PixelExtension(string name) : MarkupExtension
{
    public string Name { get; set; } = name;

    public override object ProvideValue(IServiceProvider serviceProvider) => PixelIcons.Get(Name);
}
