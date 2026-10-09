using System.Text.RegularExpressions;
using Itorrent.Core.Localization;
using Itorrent.Core.Stats;

namespace Itorrent.Tests;

public partial class LocalizationTests
{
    [Fact]
    public void Every_text_exists_in_both_languages_with_the_same_placeholders()
    {
        foreach (var (key, (pt, en)) in Strings.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(pt), $"{key}: falta português");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{key}: falta inglês");
            Assert.Equal(Placeholders(pt), Placeholders(en));
        }
    }

    [Fact]
    public void Every_key_used_in_the_source_exists()
    {
        var src = Path.Combine(RepoRoot(), "src");
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal))
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in KeyUsage().Matches(text))
                used.Add(m.Groups["key"].Value);
        }

        Assert.NotEmpty(used);
        var missing = used.Where(k => !Strings.All.ContainsKey(k)).Order().ToList();
        Assert.True(missing.Count == 0, "Chaves sem tradução: " + string.Join(", ", missing));
    }

    [Fact]
    public void Switching_language_changes_texts_and_number_format()
    {
        var before = Strings.Language;
        try
        {
            Strings.SetLanguage("pt-BR");
            Assert.Equal("Concluído", Strings.T("Status.Completed"));
            Assert.Equal("1,5 KB", Format.Bytes(1536));

            Strings.SetLanguage("en-US");
            Assert.Equal(Strings.English, Strings.Language);
            Assert.Equal("Completed", Strings.T("Status.Completed"));
            Assert.Equal("1.5 KB", Format.Bytes(1536));
            Assert.Equal("Remove \"x\" from the list?", Strings.T("Remove.Question", "x"));
        }
        finally
        {
            Strings.SetLanguage(before);
        }
    }

    [Theory]
    [InlineData("pt", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("EN-gb", "en")]
    [InlineData("de-DE", "")]
    [InlineData(null, "")]
    public void Language_codes_are_normalized(string? code, string expected) =>
        Assert.Equal(expected, Strings.Normalize(code));

    [Fact]
    public void Unknown_key_falls_back_to_the_key_itself() =>
        Assert.Equal("Nao.Existe", Strings.T("Nao.Existe"));

    private static string[] Placeholders(string s) =>
        [.. PlaceholderRegex().Matches(s).Select(m => m.Value).Distinct().Order()];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Itorrent.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Itorrent.sln não encontrado");
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderRegex();

    // Strings.T("Chave"…), {l:L Chave} no XAML e Item("Chave"…) / Loc.Bind("Chave") no menu da bandeja.
    [GeneratedRegex(@"(?:Strings\.T\(""|\{l:L |Item\(""|Loc\.Bind\("")(?<key>[A-Z][A-Za-z0-9]*\.[A-Za-z0-9.]+)")]
    private static partial Regex KeyUsage();
}
