using Itorrent.Core.Stats;
using MonoTorrent.Client;

namespace Itorrent.Core.Policies;

/// <summary>O mínimo que a política precisa controlar num torrent (permite testar sem rede).</summary>
public interface ITorrentControl
{
    Task StopAsync();
}

/// <summary>
/// Regras automáticas: ao concluir o torrent para, mantendo os arquivos no disco, a menos que
/// o usuário peça para semear (sempre ou só naquele torrent); aí semeia até a meta de ratio.
/// O upload continua ligado durante o download (tit-for-tat).
/// </summary>
public sealed class PolicyEngine
{
    public static bool IsCompletion(TorrentState oldState, TorrentState newState) =>
        newState == TorrentState.Seeding && oldState != TorrentState.Seeding;

    /// <summary>
    /// Com arquivos desmarcados o motor pode não entrar em Seeding; o progresso parcial resolve.
    /// </summary>
    public static bool IsPartialCompletion(TorrentState state, double partialProgress) =>
        state == TorrentState.Downloading && partialProgress >= 100.0;

    /// <summary>
    /// Torrent que conta como "baixando alguma coisa" para o encerramento automático.
    /// Procurando peers e semeando também contam: fechar o app interromperia o trabalho.
    /// </summary>
    public static bool KeepsAppBusy(TorrentStatus status) =>
        status is not (TorrentStatus.Paused or TorrentStatus.Completed or TorrentStatus.Error);

    /// <summary>Hora de encerrar o app por inatividade? (0 minutos = nunca)</summary>
    public static bool ShouldAutoExit(DateTimeOffset idleSince, DateTimeOffset now, int idleMinutes) =>
        idleMinutes > 0 && now - idleSince >= TimeSpan.FromMinutes(idleMinutes);

    /// <summary>
    /// Ratio = enviado ÷ baixado. A base é o maior entre o que foi baixado e o que já está no disco:
    /// torrents baixados por versões sem contador (baixado = 0) não ficam com ratio infinito.
    /// </summary>
    public static double ShareRatio(long uploaded, long downloaded, long have)
    {
        var basis = Math.Max(downloaded, have);
        return basis <= 0 ? 0 : Math.Max(0, uploaded) / (double)basis;
    }

    /// <summary>Quanto ainda falta enviar para chegar à meta (0 se já chegou ou se não há meta).</summary>
    public static long BytesToGoal(long uploaded, long downloaded, long have, double goal)
    {
        if (goal <= 0)
            return 0;
        var target = (long)Math.Ceiling(Math.Max(downloaded, have) * goal);
        return Math.Max(0, target - Math.Max(0, uploaded));
    }

    /// <summary>Chegou na meta de ratio? (meta 0 = semeia sem limite)</summary>
    public static bool ReachedRatioGoal(double ratio, double goal) => goal > 0 && ratio >= goal;

    /// <summary>Concluiu: para o torrent (não fica semeando).</summary>
    public static async Task OnCompletedAsync(ITorrentControl torrent)
    {
        ArgumentNullException.ThrowIfNull(torrent);
        await torrent.StopAsync();
    }
}
