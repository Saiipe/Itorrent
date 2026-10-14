using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using MonoTorrent;
using MonoTorrent.PieceWriter;
using ReusableTasks;

namespace Itorrent.Core.Engine;

/// <summary>
/// Pedaços de arquivos desmarcados ficam fora da pasta do download.
/// No BitTorrent os dados são baixados em peças de tamanho fixo, e uma peça pode cobrir o fim
/// de um arquivo marcado e o começo de um desmarcado. O motor baixa a peça inteira e gravaria
/// essa sobra no arquivo desmarcado, que apareceria na pasta com uma porcentagem baixada.
/// Aqui a sobra vai para a pasta de cache ("parts"). Se o arquivo for marcado depois, a sobra
/// volta para o lugar certo antes da próxima leitura/escrita.
/// </summary>
public sealed class PartsStore
{
    private readonly string _directory;
    private readonly ConcurrentDictionary<string, byte> _parked = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<ITorrentManagerFile, PartsFile> _proxies = new();

    public PartsStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        foreach (var path in Directory.EnumerateFiles(directory, "*.part"))
            _parked.TryAdd(Path.GetFileNameWithoutExtension(path), 0);
    }

    /// <summary>Há sobra guardada para este arquivo?</summary>
    public bool IsParked(ITorrentManagerFile file) => _parked.ContainsKey(KeyOf(file.FullPath));

    /// <summary>Apaga as sobras guardadas destes arquivos (torrent removido da lista).</summary>
    public void Delete(IEnumerable<ITorrentManagerFile> files)
    {
        foreach (var file in files)
        {
            var key = KeyOf(file.FullPath);
            if (!_parked.TryRemove(key, out _))
                continue;
            try
            {
                File.Delete(PathOf(key));
            }
            catch (IOException)
            {
                // Ainda aberto: fica para a próxima limpeza.
            }
        }
    }

    internal PartsFile ProxyOf(ITorrentManagerFile file) =>
        _proxies.GetValue(file, f => new PartsFile(f, PathOf(KeyOf(f.FullPath))));

    internal void MarkParked(ITorrentManagerFile file) => _parked.TryAdd(KeyOf(file.FullPath), 0);

    internal void Unpark(ITorrentManagerFile file) => _parked.TryRemove(KeyOf(file.FullPath), out _);

    private string PathOf(string key) => Path.Combine(_directory, key + ".part");

    private static string KeyOf(string fullPath) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(fullPath).ToUpperInvariant())))[..32];
}

/// <summary>Envolve o DiskWriter do MonoTorrent e desvia os arquivos desmarcados para o <see cref="PartsStore"/>.</summary>
public sealed class PartsPieceWriter(IPieceWriter inner, PartsStore store) : IPieceWriter
{
    private readonly SemaphoreSlim _moveGate = new(1, 1);

    public int OpenFiles => inner.OpenFiles;
    public int MaximumOpenFiles => inner.MaximumOpenFiles;

    public async ReusableTask CloseAsync(ITorrentManagerFile file)
    {
        await inner.CloseAsync(file);
        await inner.CloseAsync(store.ProxyOf(file));
    }

    public async ReusableTask<bool> ExistsAsync(ITorrentManagerFile file) => await inner.ExistsAsync(await RouteAsync(file));

    public async ReusableTask FlushAsync(ITorrentManagerFile file) => await inner.FlushAsync(await RouteAsync(file));

    public async ReusableTask MoveAsync(ITorrentManagerFile file, string fullPath, bool overwrite) =>
        await inner.MoveAsync(await RouteAsync(file), fullPath, overwrite);

    public async ReusableTask<int> ReadAsync(ITorrentManagerFile file, long offset, Memory<byte> buffer) =>
        await inner.ReadAsync(await RouteAsync(file), offset, buffer);

    public async ReusableTask WriteAsync(ITorrentManagerFile file, long offset, ReadOnlyMemory<byte> buffer)
    {
        var target = await RouteAsync(file);
        if (target is PartsFile)
            store.MarkParked(file);
        await inner.WriteAsync(target, offset, buffer);
    }

    public ReusableTask SetMaximumOpenFilesAsync(int maximumOpenFiles) => inner.SetMaximumOpenFilesAsync(maximumOpenFiles);

    public void Dispose() => inner.Dispose();

    /// <summary>
    /// Desmarcado e ainda não existe na pasta: usa a sobra no cache. Marcado (ou já existia na pasta):
    /// usa o arquivo de verdade, trazendo antes a sobra guardada, se houver.
    /// </summary>
    private async ReusableTask<ITorrentManagerFile> RouteAsync(ITorrentManagerFile file)
    {
        if (file.Priority == Priority.DoNotDownload)
            return File.Exists(file.FullPath) ? file : store.ProxyOf(file);
        if (!store.IsParked(file))
            return file;

        await _moveGate.WaitAsync();
        try
        {
            var proxy = store.ProxyOf(file);
            if (store.IsParked(file) && !File.Exists(file.FullPath))
            {
                await inner.CloseAsync(proxy);
                if (File.Exists(proxy.FullPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath)!);
                    File.Move(proxy.FullPath, file.FullPath);
                }
            }
            else if (File.Exists(proxy.FullPath))
            {
                // O arquivo de verdade já estava na pasta: ele vale, a sobra sai.
                await inner.CloseAsync(proxy);
                File.Delete(proxy.FullPath);
            }
            store.Unpark(file);
        }
        finally
        {
            _moveGate.Release();
        }
        return file;
    }
}

/// <summary>O mesmo arquivo do torrent, mas gravado no caminho da sobra.</summary>
internal sealed class PartsFile(ITorrentManagerFile file, string partsPath) : ITorrentManagerFile
{
    public string Path => file.Path;
    public int StartPieceIndex => file.StartPieceIndex;
    public int EndPieceIndex => file.EndPieceIndex;
    public int PieceCount => file.PieceCount;
    public long Length => file.Length;
    public long Padding => file.Padding;
    public long OffsetInTorrent => file.OffsetInTorrent;
    public MerkleRoot PiecesRoot => file.PiecesRoot;
    public ReadOnlyBitField BitField => file.BitField;
    public string FullPath => partsPath;
    public string DownloadCompleteFullPath => partsPath;
    public string DownloadIncompleteFullPath => partsPath;
    public Priority Priority => file.Priority;
}
