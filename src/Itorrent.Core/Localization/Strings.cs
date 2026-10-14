using System.Globalization;

namespace Itorrent.Core.Localization;

/// <summary>
/// Textos da interface em português e inglês. Cada entrada tem os dois idiomas lado a lado,
/// então não existe texto "esquecido" em um deles (um teste confere isso).
/// Logs continuam em português: são para diagnóstico, não para o usuário.
/// </summary>
public static class Strings
{
    public const string Portuguese = "pt-BR";
    public const string English = "en";

    public static readonly IReadOnlyList<string> Supported = [Portuguese, English];

    public static string Language { get; private set; } = Portuguese;

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("pt-BR");

    public static event EventHandler? LanguageChanged;

    /// <summary>Converte "pt", "pt-BR", "en-US"… para um idioma suportado (ou vazio).</summary>
    public static string Normalize(string? code) =>
        code is null ? ""
        : code.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? Portuguese
        : code.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? English
        : "";

    /// <summary>Idioma do Windows, se suportado; senão inglês.</summary>
    public static string FromSystem() =>
        Normalize(CultureInfo.CurrentUICulture.Name) is { Length: > 0 } lang ? lang : English;

    public static void SetLanguage(string? code)
    {
        var lang = Normalize(code);
        if (lang.Length == 0)
            lang = FromSystem();
        if (lang == Language)
            return;
        Language = lang;
        Culture = CultureInfo.GetCultureInfo(lang == English ? "en-US" : "pt-BR");
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string key) =>
        Table.TryGetValue(key, out var v) ? (Language == English ? v.En : v.Pt) : key;

    public static string T(string key, params object?[] args) =>
        string.Format(Culture, T(key), args);

    public static IReadOnlyDictionary<string, (string Pt, string En)> All => Table;

    private static readonly Dictionary<string, (string Pt, string En)> Table = new(StringComparer.Ordinal)
    {
        // ===== Geral =====
        ["App.Title"] = ("Itorrent - Gerenciador de Transferências", "Itorrent - Transfer Manager"),
        ["Common.Ok"] = ("OK", "OK"),
        ["Common.Cancel"] = ("Cancelar", "Cancel"),
        ["App.StartFailed"] = ("Não foi possível iniciar o Itorrent.\n\n{0}\n\nSe a porta estiver em uso, troque-a em Opções > Configurações.",
                               "Itorrent could not start.\n\n{0}\n\nIf the port is in use, change it in Options > Settings."),
        ["App.Unexpected"] = ("Ocorreu um erro inesperado:\n{0}", "An unexpected error occurred:\n{0}"),

        // ===== Barra de título =====
        ["Caption.Minimize"] = ("Minimizar", "Minimize"),
        ["Caption.Maximize"] = ("Maximizar", "Maximize"),
        ["Caption.Restore"] = ("Restaurar", "Restore"),
        ["Caption.Close"] = ("Fechar", "Close"),

        // ===== Menus =====
        ["Menu.File"] = ("_Arquivo", "_File"),
        ["Menu.OpenTorrent"] = ("Abrir arquivo ._torrent...", "Open ._torrent file..."),
        ["Menu.AddMagnet"] = ("Adicionar link _magnet...", "Add _magnet link..."),
        ["Menu.HideToTray"] = ("_Ocultar na bandeja", "_Hide to tray"),
        ["Menu.Exit"] = ("_Sair", "E_xit"),
        ["Menu.Transfers"] = ("_Transferências", "_Transfers"),
        ["Menu.Pause"] = ("_Pausar", "_Pause"),
        ["Menu.Resume"] = ("_Retomar", "_Resume"),
        ["Menu.Redownload"] = ("_Baixar de novo / escolher arquivos...", "_Download again / choose files..."),
        ["Menu.Remove"] = ("Re_mover...", "Re_move..."),
        ["Menu.OpenFolder"] = ("Abrir _pasta", "Open _folder"),
        ["Menu.Reannounce"] = ("Procurar _peers de novo", "Search for _peers again"),
        ["Menu.PauseAll"] = ("Pausar _tudo", "Pause _all"),
        ["Menu.ResumeAll"] = ("Retomar t_udo", "Resume a_ll"),
        ["Menu.Options"] = ("_Opções", "_Options"),
        ["Menu.Turbo"] = ("Modo _Turbo", "_Turbo mode"),
        ["Menu.TestConnection"] = ("Testar _conexão...", "Test _connection..."),
        ["Menu.Settings"] = ("_Configurações...", "_Settings..."),
        ["Menu.Window"] = ("_Janela", "_Window"),
        ["Menu.FilterAll"] = ("1 _Todos", "1 _All"),
        ["Menu.FilterDownloading"] = ("2 _Baixando", "2 _Downloading"),
        ["Menu.FilterCompleted"] = ("3 _Concluídos", "3 _Completed"),
        ["Menu.FilterPaused"] = ("4 _Pausados", "4 _Paused"),
        ["Menu.About"] = ("_Sobre o Itorrent...", "_About Itorrent..."),

        // ===== Barra de ferramentas =====
        ["Tool.TurboTip"] = ("Modo Turbo: liga UPnP, DHT, PEX, descoberta local, trackers públicos e até 500 conexões por torrent.",
                             "Turbo mode: turns on UPnP, DHT, PEX, local discovery, public trackers and up to 500 connections per torrent."),
        ["Tool.OpenTip"] = ("Abrir arquivo .torrent (Ctrl+O)", "Open .torrent file (Ctrl+O)"),
        ["Tool.Open"] = ("Abrir", "Open"),
        ["Tool.MagnetTip"] = ("Adicionar link magnet (Ctrl+M)", "Add magnet link (Ctrl+M)"),
        ["Tool.Magnet"] = ("Magnet", "Magnet"),
        ["Tool.Resume"] = ("Retomar", "Resume"),
        ["Tool.Pause"] = ("Pausar", "Pause"),
        ["Tool.RemoveTip"] = ("Remover (Del)", "Remove (Del)"),
        ["Tool.Remove"] = ("Remover", "Remove"),
        ["Tool.FolderTip"] = ("Abrir a pasta do download", "Open the download folder"),
        ["Tool.Folder"] = ("Pasta", "Folder"),
        ["Tool.Options"] = ("Opções", "Options"),
        ["Tool.Connection"] = ("Conexão", "Network"),
        ["Tool.ConnectionTip"] = ("Testar a conexão e reconectar se ela caiu", "Test the connection and reconnect if it dropped"),

        // ===== Janela principal =====
        ["Col.Name"] = ("Nome", "Name"),
        ["Col.Size"] = ("Tamanho", "Size"),
        ["Col.Progress"] = ("Progresso", "Progress"),
        ["Col.Status"] = ("Status", "Status"),
        ["Col.Down"] = ("Down", "Down"),
        ["Col.Up"] = ("Up", "Up"),
        ["Col.Time"] = ("Tempo", "ETA"),
        ["Col.Seeds"] = ("Seeds", "Seeds"),
        ["Col.Peers"] = ("Peers", "Peers"),
        ["Col.Added"] = ("Adicionado", "Added"),
        ["Col.File"] = ("Arquivo", "File"),
        ["Col.Alert"] = ("Alerta", "Warning"),
        ["Col.Get"] = ("Baixar", "Get"),
        ["File.DeleteTip"] = ("Mover este arquivo para a Lixeira", "Move this file to the Recycle Bin"),
        ["File.Missing"] = ("apagado", "deleted"),
        ["File.MissingAlert"] = ("Apagado do disco", "Deleted from disk"),
        ["Delete.Title"] = ("Apagar Arquivo", "Delete File"),
        ["Delete.Question"] = ("Mover \"{0}\" para a Lixeira?", "Move \"{0}\" to the Recycle Bin?"),
        ["Delete.Note"] = ("O arquivo é desmarcado e deixa de ser baixado. Dá para recuperá-lo pela Lixeira do Windows, ou baixá-lo de novo marcando a caixinha.",
                           "The file is unchecked and no longer downloaded. You can restore it from the Windows Recycle Bin, or download it again by checking its box."),
        ["Delete.Button"] = ("Apagar", "Delete"),
        ["File.GetTip"] = ("Marque para baixar este arquivo; desmarque para não baixar.", "Check to download this file; uncheck to skip it."),
        ["Main.RiskyTip"] = ("Contém arquivos executáveis", "Contains executable files"),
        ["Main.Empty"] = ("Nenhuma transferência.", "No transfers."),
        ["Main.EmptyHint"] = ("Clique em Magnet, abra um .torrent ou arraste-o para esta janela.",
                              "Click Magnet, open a .torrent or drag one onto this window."),
        ["Main.Info"] = ("Informações", "Information"),
        ["Main.SelectHint"] = ("Selecione uma transferência.", "Select a transfer."),
        ["Main.Transfers"] = ("Transferências", "Transfers"),
        ["Main.TransfersDownloading"] = ("Transferências — Baixando", "Transfers — Downloading"),
        ["Main.TransfersCompleted"] = ("Transferências — Concluídos", "Transfers — Completed"),
        ["Main.TransfersPaused"] = ("Transferências — Pausados", "Transfers — Paused"),
        ["Main.Files"] = ("Arquivos", "Files"),
        ["Main.FilesOf"] = ("Arquivos — {0}", "Files — {0}"),
        ["Main.OnlyMagnet"] = ("O Itorrent só abre links magnet e arquivos .torrent.", "Itorrent only opens magnet links and .torrent files."),
        ["Main.FolderMissing"] = ("A pasta ainda não existe.", "The folder does not exist yet."),
        ["Main.TurboFailed"] = ("Não foi possível aplicar o Modo Turbo: {0}", "Could not apply Turbo mode: {0}"),
        ["Main.SettingsFailed"] = ("Algumas configurações não puderam ser aplicadas: {0}", "Some settings could not be applied: {0}"),
        ["Info.Status"] = ("Status:", "Status:"),
        ["Info.Downloaded"] = ("Baixado:", "Downloaded:"),
        ["Info.Progress"] = ("Progresso:", "Progress:"),
        ["Info.Speed"] = ("Velocidade:", "Speed:"),
        ["Info.Eta"] = ("Tempo restante:", "Time left:"),
        ["Info.Seeds"] = ("Seeds:", "Seeds:"),
        ["Info.SeedsTip"] = ("Conectados a você (total na rede, pelo tracker)", "Connected to you (total in the swarm, from the tracker)"),
        ["Info.Peers"] = ("Peers: ", "Peers: "),
        ["Info.Folder"] = ("Pasta:", "Folder:"),
        ["Info.Added"] = ("Adicionado:", "Added:"),
        ["Info.Risky"] = ("Contém executáveis. O Itorrent nunca os abre; os arquivos recebem a marca 'baixado da internet' (SmartScreen).",
                          "Contains executables. Itorrent never opens them; the files get the 'downloaded from the internet' mark (SmartScreen)."),
        ["Group.All"] = ("Todos", "All"),
        ["Group.Downloading"] = ("Baixando", "Downloading"),
        ["Group.Completed"] = ("Concluídos", "Completed"),
        ["Group.Paused"] = ("Pausados", "Paused"),

        // ===== Barra de status =====
        ["StatusBar.Torrents"] = ("Torrents: {0} ativos / {1}", "Torrents: {0} active / {1}"),
        ["StatusBar.Port"] = ("Porta: {0}", "Port: {0}"),
        ["StatusBar.Dht"] = ("DHT: {0}", "DHT: {0}"),
        ["Dht.Ready"] = ("pronto", "ready"),
        ["Dht.Starting"] = ("iniciando", "starting"),
        ["Dht.Off"] = ("desligado", "off"),
        ["StatusBar.Online"] = ("Conectado", "Online"),
        ["StatusBar.Offline"] = ("SEM REDE", "OFFLINE"),
        ["StatusBar.Reconnecting"] = ("Reconectando...", "Reconnecting..."),
        ["StatusBar.ConnectionTip"] = ("Clique para testar a conexão. Se a rede cair, o Itorrent reconecta sozinho quando ela voltar.",
                                       "Click to test the connection. If the network drops, Itorrent reconnects by itself when it comes back."),
        ["StatusBar.Turbo"] = ("MODO TURBO", "TURBO MODE"),
        ["StatusBar.Normal"] = ("Normal", "Normal"),

        // ===== Status dos torrents =====
        ["Status.Starting"] = ("Iniciando", "Starting"),
        ["Status.FetchingMetadata"] = ("Obtendo metadados", "Getting metadata"),
        ["Status.Checking"] = ("Verificando", "Checking"),
        ["Status.Downloading"] = ("Baixando", "Downloading"),
        ["Status.Stalled"] = ("Procurando peers", "Looking for peers"),
        ["Status.Seeding"] = ("Semeando", "Seeding"),
        ["Status.Paused"] = ("Pausado", "Paused"),
        ["Status.Completed"] = ("Concluído", "Completed"),
        ["Status.Stopping"] = ("Parando", "Stopping"),
        ["Status.Error"] = ("Erro", "Error"),
        ["Item.DownloadedOf"] = ("{0} de {1}", "{0} of {1}"),
        ["File.Ignored"] = ("ignorado", "skipped"),
        ["File.DoubleExt"] = ("Extensão dupla!", "Double extension!"),
        ["File.Executable"] = ("Executável", "Executable"),

        // ===== Novo download / baixar novamente =====
        ["Add.TitleNew"] = ("Novo Download", "New Download"),
        ["Add.TitleAgain"] = ("Baixar Novamente", "Download Again"),
        ["Add.Loading"] = ("Obtendo informações do torrent...", "Getting torrent information..."),
        ["Add.TotalSize"] = ("Tamanho total: ", "Total size: "),
        ["Add.Files"] = ("Arquivos: ", "Files: "),
        ["Add.Private"] = ("Privado: ", "Private: "),
        ["Add.FileCount"] = ("{0} arquivo(s)", "{0} file(s)"),
        ["Add.PrivateYes"] = ("Sim (sem DHT/PEX)", "Yes (no DHT/PEX)"),
        ["Add.PrivateNo"] = ("Não", "No"),
        ["Add.RiskTitle"] = ("ATENÇÃO: arquivos executáveis", "WARNING: executable files"),
        ["Add.RiskMore"] = (" e mais {0}", " and {0} more"),
        ["Add.RiskDouble"] = ("{0} arquivo(s) com EXTENSÃO DUPLA, típico de vírus disfarçado. ",
                              "{0} file(s) with a DOUBLE EXTENSION, typical of disguised viruses. "),
        ["Add.RiskBody"] = ("Este torrent contém programas ou scripts ({0}). O Itorrent nunca executa arquivos baixados; desmarque-os se não confiar na origem.",
                            "This torrent contains programs or scripts ({0}). Itorrent never runs downloaded files; uncheck them if you don't trust the source."),
        ["Add.AgainHint"] = ("Marque os arquivos que quer baixar. Antes de começar, o Itorrent confere o que já está no disco: o que estiver íntegro não é baixado de novo, e o que foi apagado volta a ser baixado.",
                             "Check the files you want. Before starting, Itorrent checks what is already on disk: intact files are not downloaded again, and deleted ones are downloaded again."),
        ["Add.SelectAll"] = ("_Marcar todos", "_Select all"),
        ["Add.SelectNone"] = ("_Desmarcar todos", "_Deselect all"),
        ["Add.ToDownload"] = ("Arquivos que serão baixados:", "Files to download:"),
        ["Add.Fetching"] = ("Obtendo a lista de arquivos dos peers...", "Getting the file list from peers..."),
        ["Add.FetchingHint"] = ("Isso pode levar alguns segundos (DHT e trackers).", "This may take a few seconds (DHT and trackers)."),
        ["Add.Elapsed"] = ("Procurando há {0}", "Searching for {0}"),
        ["Add.SlowHint"] = ("Está demorando? Clique em \"Tentar novamente\" para procurar peers de novo.",
                            "Taking too long? Click \"Try again\" to search for peers again."),
        ["Add.Retry"] = ("_Tentar novamente", "_Try again"),
        ["Add.Selection"] = ("Selecionados: {0} de {1} arquivo(s) — {2}", "Selected: {0} of {1} file(s) — {2}"),
        ["Add.SaveTo"] = ("Salvar em", "Save to"),
        ["Add.Browse"] = ("_Procurar...", "_Browse..."),
        ["Add.MakeDefault"] = ("Usar como pasta _padrão", "Use as _default folder"),
        ["Add.InvalidFolder"] = ("Pasta inválida", "Invalid folder"),
        ["Add.FreeSpace"] = ("Livre em {0}: {1}", "Free on {0}: {1}"),
        ["Add.DriveUnavailable"] = ("Unidade indisponível", "Drive unavailable"),
        ["Add.Download"] = ("_Baixar", "_Download"),
        ["Add.MetadataFailed"] = ("Não foi possível obter a lista de arquivos. Verifique sua conexão e tente novamente.",
                                  "Could not get the file list. Check your connection and try again."),
        ["Add.StartFailed"] = ("Não foi possível iniciar o download: {0}", "Could not start the download: {0}"),

        // ===== Configurações =====
        ["Settings.Title"] = ("Configurações", "Settings"),
        ["Settings.Language"] = ("Idioma", "Language"),
        ["Settings.Downloads"] = ("Downloads", "Downloads"),
        ["Settings.DefaultFolder"] = ("Pasta padrão:", "Default folder:"),
        ["Settings.Notify"] = ("Avisar quando um download terminar", "Notify when a download finishes"),
        ["Settings.PreventSleep"] = ("Impedir suspensão do PC durante downloads", "Keep the PC awake while downloading"),
        ["Settings.Speed"] = ("Velocidade", "Speed"),
        ["Settings.MaxDown"] = ("Limite de download (KB/s):", "Download limit (KB/s):"),
        ["Settings.MaxUp"] = ("Limite de upload (KB/s):", "Upload limit (KB/s):"),
        ["Settings.Conns"] = ("Conexões por torrent:", "Connections per torrent:"),
        ["Settings.TurboConns"] = ("Conexões no Turbo (200-500):", "Connections in Turbo (200-500):"),
        ["Settings.OnClose"] = ("Ao fechar", "On close"),
        ["Settings.CloseClick"] = ("Ao clicar no ✕ da janela:", "When clicking the window ✕:"),
        ["Settings.KeepBackground"] = ("Continuar em segundo plano (bandeja)", "Keep running in the background (tray)"),
        ["Settings.CloseApp"] = ("Fechar o Itorrent", "Close Itorrent"),
        ["Settings.AutoExit"] = ("Encerrar sozinho se ficar sem baixar nada por:", "Exit automatically when idle for:"),
        ["Settings.Min30"] = ("30 min", "30 min"),
        ["Settings.H2"] = ("2 horas", "2 hours"),
        ["Settings.H3"] = ("3 horas", "3 hours"),
        ["Settings.Never"] = ("Nunca", "Never"),
        ["Settings.AutoExitHint"] = ("Só vale com a janela escondida ou minimizada. Downloads pausados ou concluídos não contam como atividade.",
                                     "Only applies while the window is hidden or minimized. Paused or completed downloads don't count as activity."),
        ["Settings.Connection"] = ("Conexão", "Connection"),
        ["Settings.Port"] = ("Porta do BitTorrent:", "BitTorrent port:"),
        ["Settings.Upnp"] = ("UPnP / NAT-PMP (abrir porta no roteador)", "UPnP / NAT-PMP (open port on router)"),
        ["Settings.Dht"] = ("DHT (rede sem tracker)", "DHT (trackerless network)"),
        ["Settings.Pex"] = ("PEX (troca de peers)", "PEX (peer exchange)"),
        ["Settings.Lpd"] = ("Descoberta de peers na rede local", "Local peer discovery"),
        ["Settings.PublicTrackers"] = ("Adicionar trackers públicos", "Add public trackers"),
        ["Settings.Turbo"] = ("Modo Turbo", "Turbo mode"),
        ["Settings.TurboHint"] = ("Liga tudo acima e sobe os limites de conexão. Nenhum programa passa da velocidade contratada; o Turbo ajuda a usar 100% dela.",
                                  "Turns on everything above and raises connection limits. No program goes beyond your contracted speed; Turbo helps you use 100% of it."),
        ["Settings.System"] = ("Sistema", "System"),
        ["Settings.StartWithWindows"] = ("Iniciar com o Windows (na bandeja)", "Start with Windows (in the tray)"),
        ["Settings.Associate"] = ("Abrir _magnet e .torrent com o Itorrent", "Open _magnet and .torrent with Itorrent"),
        ["Settings.Associated"] = ("Links magnet e arquivos .torrent abrem no Itorrent.", "Magnet links and .torrent files open in Itorrent."),
        ["Settings.NotAssociated"] = ("Links magnet ainda não abrem no Itorrent.", "Magnet links don't open in Itorrent yet."),
        ["Settings.AssociateFailed"] = ("Não foi possível associar: {0}", "Could not associate: {0}"),
        ["Settings.ErrFolder"] = ("Pasta padrão inválida. Use um caminho completo, ex.: C:\\Downloads", "Invalid default folder. Use a full path, e.g. C:\\Downloads"),
        ["Settings.ErrPort"] = ("Porta deve estar entre 1024 e 65535.", "Port must be between 1024 and 65535."),
        ["Settings.ErrConns"] = ("Conexões por torrent: entre 10 e 1000.", "Connections per torrent: between 10 and 1000."),
        ["Settings.ErrTurboConns"] = ("Conexões no Modo Turbo: entre 200 e 500.", "Turbo mode connections: between 200 and 500."),
        ["Settings.ErrSpeed"] = ("Limites de velocidade devem ser números (0 = sem limite).", "Speed limits must be numbers (0 = unlimited)."),

        // ===== Caixas de diálogo =====
        ["Magnet.Title"] = ("Adicionar Link Magnet", "Add Magnet Link"),
        ["Magnet.Prompt"] = ("Cole um ou mais links magnet (um por linha):", "Paste one or more magnet links (one per line):"),
        ["Magnet.InvalidLine"] = ("A linha {0} não é um link magnet válido.", "Line {0} is not a valid magnet link."),
        ["Magnet.TooMany"] = ("No máximo {0} links de uma vez.", "At most {0} links at once."),
        ["Magnet.Invalid"] = ("Isto não parece um link magnet válido (deve começar com magnet:?xt=...).",
                              "This doesn't look like a valid magnet link (it must start with magnet:?xt=...)."),
        ["Remove.Title"] = ("Remover Transferência", "Remove Transfer"),
        ["Remove.Question"] = ("Remover \"{0}\" da lista?", "Remove \"{0}\" from the list?"),
        ["Remove.DeleteFiles"] = ("Apagar também os arquivos baixados do disco", "Also delete the downloaded files from disk"),
        ["About.Title"] = ("Sobre o Itorrent", "About Itorrent"),
        ["About.Body"] = ("Versão {0}\nCliente BitTorrent para Windows\nMotor: MonoTorrent · .NET 10",
                          "Version {0}\nBitTorrent client for Windows\nEngine: MonoTorrent · .NET 10"),
        ["About.Legal"] = ("Criar e usar um cliente BitTorrent é legal. Baixar ou compartilhar conteúdo protegido por direitos autorais sem autorização não é, e a responsabilidade é de quem usa.\n\nLembre-se: todo peer do swarm vê o seu endereço IP.",
                           "Creating and using a BitTorrent client is legal. Downloading or sharing copyrighted content without permission is not, and the responsibility lies with the user.\n\nRemember: every peer in the swarm can see your IP address."),
        ["Pick.Folder"] = ("Escolha onde salvar", "Choose where to save"),
        ["Pick.Torrent"] = ("Abrir arquivo .torrent", "Open .torrent file"),
        ["Pick.Filter"] = ("Arquivos torrent (*.torrent)|*.torrent", "Torrent files (*.torrent)|*.torrent"),

        // ===== Teste de conexão =====
        ["Conn.Title"] = ("Teste de Conexão", "Connection Test"),
        ["Conn.Testing"] = ("Testando a conexão e reconectando...", "Testing the connection and reconnecting..."),
        ["Conn.TestingHint"] = ("Leva até uns 30 segundos.", "Takes up to about 30 seconds."),
        ["Conn.AllOk"] = ("Tudo certo: você está conectado.", "All good: you are connected."),
        ["Conn.SomeWarnings"] = ("Conectado, com alguns avisos.", "Connected, with a few warnings."),
        ["Conn.HasFailures"] = ("Há problemas na conexão.", "There are connection problems."),
        ["Conn.AutoNote"] = ("Se a rede cair, o Itorrent reconecta sozinho quando ela voltar.",
                             "If the network drops, Itorrent reconnects by itself when it comes back."),
        ["Conn.Again"] = ("_Testar de novo", "_Test again"),
        ["Conn.Error"] = ("O teste falhou: {0}", "The test failed: {0}"),
        ["Conn.Network"] = ("Rede", "Network"),
        ["Conn.NetworkOk"] = ("Cabo/Wi-Fi conectado.", "Cable/Wi-Fi connected."),
        ["Conn.NetworkFail"] = ("Nenhuma rede conectada. Confira o cabo ou o Wi-Fi.", "No network connected. Check the cable or Wi-Fi."),
        ["Conn.Internet"] = ("Internet", "Internet"),
        ["Conn.InternetOk"] = ("{0} de {1} servidores de trackers encontrados.", "{0} of {1} tracker servers found."),
        ["Conn.InternetFail"] = ("Sem internet (ou o DNS não responde). Confira o roteador.", "No internet (or DNS is not answering). Check the router."),
        ["Conn.Port"] = ("Porta", "Port"),
        ["Conn.PortOk"] = ("Porta {0} recebendo conexões neste PC.", "Port {0} accepting connections on this PC."),
        ["Conn.PortFail"] = ("A porta {0} não está aberta. Outro programa pode estar usando; troque em Configurações.",
                             "Port {0} is not open. Another program may be using it; change it in Settings."),
        ["Conn.Router"] = ("Roteador", "Router"),
        ["Conn.RouterOff"] = ("Encaminhamento automático (UPnP) desligado.", "Automatic port forwarding (UPnP) is off."),
        ["Conn.RouterOk"] = ("Porta encaminhada no roteador (UPnP/NAT-PMP).", "Port forwarded on the router (UPnP/NAT-PMP)."),
        ["Conn.RouterRefused"] = ("O roteador recusou o encaminhamento. Baixa normalmente, mas com menos peers.",
                                  "The router refused port forwarding. Downloads still work, with fewer peers."),
        ["Conn.RouterNone"] = ("Nenhum roteador com UPnP/NAT-PMP respondeu. Baixa normalmente, mas com menos peers.",
                               "No UPnP/NAT-PMP router answered. Downloads still work, with fewer peers."),
        ["Conn.Dht"] = ("DHT", "DHT"),
        ["Conn.DhtOff"] = ("Desligada nas Configurações.", "Turned off in Settings."),
        ["Conn.DhtOk"] = ("Pronta, {0} nós conhecidos.", "Ready, {0} known nodes."),
        ["Conn.DhtStarting"] = ("Iniciando ({0} nós até agora).", "Starting ({0} nodes so far)."),
        ["Conn.DhtRestarted"] = ("Estava travada e foi reiniciada; leva até um minuto para achar nós.",
                                 "It was stuck and was restarted; it takes up to a minute to find nodes."),
        ["Conn.Trackers"] = ("Trackers", "Trackers"),
        ["Conn.TrackersOk"] = ("{0} de {1} trackers responderam.", "{0} of {1} trackers answered."),
        ["Conn.TrackersFail"] = ("Nenhum dos {0} trackers respondeu. Pode ser firewall, antivírus ou bloqueio do provedor.",
                                 "None of the {0} trackers answered. It may be a firewall, antivirus or ISP block."),
        ["Conn.TrackersNone"] = ("Os downloads ativos não têm trackers (usam só DHT/PEX).", "Active downloads have no trackers (DHT/PEX only)."),
        ["Conn.Peers"] = ("Peers", "Peers"),
        ["Conn.PeersOk"] = ("{0} conexão(ões) abertas com peers.", "{0} open connection(s) with peers."),
        ["Conn.PeersNone"] = ("Nenhum peer conectado ainda; aguarde um pouco.", "No peers connected yet; give it a moment."),
        ["Conn.EngineIdle"] = ("Não testado: nada está baixando agora (a porta e a DHT só ligam com um download ativo).",
                               "Not tested: nothing is downloading now (the port and DHT only turn on with an active download)."),
        ["Conn.NoActive"] = ("Nenhum download ativo para testar.", "No active download to test."),
        ["Conn.SkippedNoNetwork"] = ("Não testado: sem rede.", "Not tested: no network."),
        ["Conn.SkippedNoInternet"] = ("Não testado: sem internet.", "Not tested: no internet."),
        ["Conn.Reconnect"] = ("Reconexão", "Reconnect"),
        ["Conn.DidAnnounce"] = ("{0} torrent(s) anunciado(s) de novo", "{0} torrent(s) announced again"),
        ["Conn.DidDht"] = ("DHT reiniciada", "DHT restarted"),
        ["Conn.DidRouter"] = ("encaminhamento de porta refeito", "port forwarding redone"),
        ["Conn.DidResume"] = ("{0} torrent(s) com erro retomado(s)", "{0} torrent(s) with errors resumed"),
        ["Conn.NothingToDo"] = ("Nada precisou ser refeito.", "Nothing needed to be redone."),

        // ===== Bandeja =====
        ["Tray.Open"] = ("_Abrir Itorrent", "_Open Itorrent"),
        ["Tray.StillRunningTitle"] = ("Itorrent continua rodando", "Itorrent is still running"),
        ["Tray.StillRunning"] = ("Os downloads seguem em segundo plano. Use o ícone da bandeja para abrir ou sair.",
                                 "Downloads keep going in the background. Use the tray icon to open or exit."),
        ["Tray.Completed"] = ("Download concluído", "Download complete"),

        // ===== Erros do motor =====
        ["Err.InvalidMagnet"] = ("Link magnet inválido ou malformado.", "Invalid or malformed magnet link."),
        ["Err.AlreadyAdded"] = ("Este torrent já está na lista de transferências.", "This torrent is already in the transfer list."),
        ["Err.BadMetadata"] = ("Os metadados recebidos dos peers são inválidos.", "The metadata received from peers is invalid."),
        ["Err.BadTorrentFile"] = ("Arquivo .torrent inválido, corrompido ou maior que 10 MB.", "Invalid, corrupted or larger than 10 MB .torrent file."),
        ["Err.InvalidFolder"] = ("Pasta de destino inválida.", "Invalid destination folder."),
        ["Err.SelectOne"] = ("Selecione pelo menos um arquivo.", "Select at least one file."),
        ["Err.PathsOutside"] = ("O torrent contém caminhos fora da pasta de download.", "The torrent contains paths outside the download folder."),
        ["Err.Security"] = ("Torrent recusado por segurança: {0}.", "Torrent rejected for security reasons: {0}."),
        ["Err.NoSpace"] = ("Espaço insuficiente no disco {0} ({1} livres, {2} necessários).", "Not enough space on drive {0} ({1} free, {2} needed)."),
        ["Err.NoFileList"] = ("Este torrent ainda não tem a lista de arquivos.", "This torrent doesn't have its file list yet."),
        ["Err.NotInList"] = ("Este torrent não está mais na lista.", "This torrent is no longer in the list."),
        ["Err.Disk"] = ("Erro de disco", "Disk error"),
        ["Err.DeleteFailed"] = ("Não foi possível apagar o arquivo: {0}", "Could not delete the file: {0}"),
        ["Path.Absolute"] = ("caminho absoluto ou com ':' não é permitido", "absolute paths or ':' are not allowed"),
        ["Path.DotDot"] = ("caminho com '..' não é permitido", "paths with '..' are not allowed"),
        ["Path.Reserved"] = ("nome reservado do Windows: {0}", "reserved Windows name: {0}"),
        ["Path.InvalidChars"] = ("nome de arquivo com caracteres inválidos", "file name with invalid characters"),
        ["Path.Outside"] = ("caminho fora da pasta de download", "path outside the download folder"),
        ["Path.TooLong"] = ("caminho acima do limite de tamanho", "path exceeds the length limit"),
    };
}
