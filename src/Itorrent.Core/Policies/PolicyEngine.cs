using MonoTorrent.Client;

namespace Itorrent.Core.Policies;

/// <summary>O mínimo que a política precisa controlar num torrent (permite testar sem rede).</summary>
public interface ITorrentControl
{
    Task StopAsync();
}

/// <summary>
/// Regras automáticas: parar de semear ao concluir, mantendo os arquivos no disco.
/// O upload continua ligado durante o download (tit-for-tat); só para depois de concluir.
/// </summary>
public sealed class PolicyEngine(Func<bool> stopSeedingOnComplete)
{
    public static bool IsCompletion(TorrentState oldState, TorrentState newState) =>
        newState == TorrentState.Seeding && oldState != TorrentState.Seeding;

    /// <summary>
    /// Com arquivos desmarcados o motor pode não entrar em Seeding; o progresso parcial resolve.
    /// </summary>
    public static bool IsPartialCompletion(TorrentState state, double partialProgress) =>
        state == TorrentState.Downloading && partialProgress >= 100.0;

    /// <returns>true se o torrent foi parado pela política.</returns>
    public async Task<bool> OnCompletedAsync(ITorrentControl torrent)
    {
        ArgumentNullException.ThrowIfNull(torrent);
        if (!stopSeedingOnComplete())
            return false;
        await torrent.StopAsync();
        return true;
    }
}
