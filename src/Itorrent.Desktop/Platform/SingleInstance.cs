using System.IO;
using System.IO.Pipes;
using System.Text;
using Serilog;

namespace Itorrent.Desktop.Platform;

/// <summary>
/// Garante uma única instância por usuário. Uma segunda instância (aberta pelo navegador
/// com um magnet, ou por clique duplo num .torrent) repassa o argumento pelo pipe e encerra.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const string ShowCommand = "\u0001SHOW";
    private const int MaxMessageBytes = 8 * 1024;

    private static readonly string Id = $"Itorrent-{Environment.UserName}";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();

    public SingleInstance()
    {
        _mutex = new Mutex(true, $@"Local\{Id}-mutex", out var created);
        IsFirst = created;
    }

    public bool IsFirst { get; }

    /// <summary>Envia o argumento para a instância principal. Retorna false se não conseguiu.</summary>
    public static bool SendToPrimary(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Id, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(3000);
            var bytes = Encoding.UTF8.GetBytes(message);
            if (bytes.Length > MaxMessageBytes)
                return false;
            client.Write(bytes);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Escuta o pipe. Tudo o que chega é texto não confiável e passa pela mesma validação.</summary>
    public void Listen(Action<string> onMessage)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        Id, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);

                    var buffer = new byte[MaxMessageBytes + 1];
                    var total = 0;
                    int read;
                    while (total < buffer.Length
                           && (read = await server.ReadAsync(buffer.AsMemory(total), _cts.Token)) > 0)
                        total += read;

                    if (total > MaxMessageBytes)
                    {
                        Log.Warning("Mensagem do pipe recusada: acima de 8 KB");
                        continue;
                    }
                    var text = Encoding.UTF8.GetString(buffer, 0, total).Trim();
                    if (text.Length > 0)
                        onMessage(text);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Falha no pipe de instância única");
                    await Task.Delay(500);
                }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (IsFirst)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
