using Serilog;

namespace Itorrent.Core.Engine;

/// <summary>
/// Segura os arquivos baixados abertos (só leitura, sem permitir exclusão): enquanto o Itorrent
/// está aberto, o Explorer não consegue apagar nem renomear esses arquivos. Abrir, ler e copiar
/// continuam liberados, e o motor continua gravando e lendo normalmente.
/// Para apagar, use a lixeira do Itorrent (solta a trava antes) ou feche o app.
/// </summary>
public sealed class FileLocks : IDisposable
{
    private readonly Dictionary<string, FileStream> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _suspended = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();
    private readonly ILogger _log = Log.ForContext<FileLocks>();

    public bool IsHeld(string path)
    {
        lock (_gate)
            return _held.ContainsKey(Path.GetFullPath(path));
    }

    /// <summary>Trava o arquivo se ele existir. Sem sucesso (em uso exclusivo por outro programa), tenta de novo depois.</summary>
    public void Hold(string path)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            if (_held.ContainsKey(path) || _suspended.ContainsKey(path) || !File.Exists(path))
                return;
            try
            {
                _held[path] = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 1);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Debug(ex, "Não foi possível travar {Path}", path);
            }
        }
    }

    public void Release(string path)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            if (_held.Remove(path, out var stream))
                stream.Dispose();
        }
    }

    /// <summary>
    /// Solta a trava e impede que <see cref="Hold"/> trave de novo até o retorno ser descartado.
    /// Use ao apagar: a Lixeira do Windows demora, e a sincronização periódica travaria no meio.
    /// </summary>
    public IDisposable Suspend(string path)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            _suspended[path] = _suspended.GetValueOrDefault(path) + 1;
            if (_held.Remove(path, out var stream))
                stream.Dispose();
        }
        return new Suspension(this, path);
    }

    private void Resume(string path)
    {
        lock (_gate)
        {
            if (_suspended.TryGetValue(path, out var count) && count > 1)
                _suspended[path] = count - 1;
            else
                _suspended.Remove(path);
        }
    }

    private sealed class Suspension(FileLocks owner, string path) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Resume(path);
        }
    }

    public void Release(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            Release(path);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var stream in _held.Values)
                stream.Dispose();
            _held.Clear();
        }
    }
}
