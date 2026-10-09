<p align="center">
  <img src="icon/png/itorrent-128x128.png" width="96" alt="Ícone do Itorrent" />
</p>

<h1 align="center">Itorrent</h1>

<p align="center">Cliente BitTorrent para Windows com cara de Windows 3.1: rápido, seguro e que para de semear quando termina.</p>

---

## O que ele faz

- **Adiciona torrents** por link magnet, arquivo `.torrent`, arrastar e soltar ou clique no navegador.
- **Mostra os arquivos antes de baixar**: você desmarca o que não quer e escolhe a pasta onde salvar.
- **Modo Turbo**: um botão que liga UPnP/NAT-PMP, DHT, PEX, descoberta local, trackers públicos e sobe o limite para até 500 conexões por torrent.
- **Para de semear ao concluir**, mantendo os arquivos no disco. O upload continua durante o download, porque o BitTorrent recompensa quem envia.
- **Progresso, velocidade, tempo restante, seeds e peers** no formato do qBittorrent: `12 (340)` = 12 conectados, 340 na rede.
- **Roda na bandeja**: fechar (✕) só esconde a janela. Pausar tudo, retomar tudo e sair ficam no menu do ícone.
- **Retoma de onde parou** depois de fechar o app ou reiniciar o PC (SQLite + fast resume).
- Opcional: iniciar com o Windows, impedir a suspensão durante downloads e notificar quando terminar.

## Segurança

| Ameaça | O que o Itorrent faz |
|---|---|
| Acesso remoto ao PC | Não há Web UI nem API. A única porta aberta é a do BitTorrent. |
| Outro processo controlando o app | Named pipe com `CurrentUserOnly`, mensagens de no máximo 8 KB, tudo revalidado. |
| Path traversal (`../../Startup/x.exe`) | `PathGuard` recusa `..`, caminhos absolutos, `:` (ADS), nomes reservados (`CON`, `NUL`…) e caminhos longos demais, e confere de novo o caminho final calculado pelo motor. |
| Executável disfarçado (`filme.mp4.exe`) | Alerta antes de baixar, o app **nunca executa** arquivos e grava o *Mark of the Web* (SmartScreen) em cada arquivo concluído. |
| Magnet ou `.torrent` malformado | Limite de 8 KB para magnet e 10 MB para `.torrent`, parse protegido e trackers só `http`, `https` e `udp`. |
| SQL injection | Todas as consultas são parametrizadas. |
| Elevação de privilégio | Roda sem administrador; registro só em `HKCU`; dados em `%LocalAppData%\Itorrent`. |

Torrents privados nunca usam DHT/PEX nem recebem trackers extras, nem com o Turbo ligado.

## Como compilar e rodar

Requisito: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build Itorrent.sln
dotnet test                                     # 64 testes
dotnet run --project src/Itorrent.Desktop
```

### Gerar o executável para distribuir

Gera um único `Itorrent.exe` que roda sem o .NET instalado:

```powershell
dotnet publish src/Itorrent.Desktop -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```

Para o instalador, abra `installer/Itorrent.iss` no [Inno Setup 6](https://jrsoftware.org/isinfo.php) e compile. Ele instala por usuário (sem administrador), registra `magnet:` e `.torrent` e cria os atalhos.

Sem o instalador, dá para associar os links magnet pelo próprio app em **Opções → Configurações → "Abrir magnet e .torrent com o Itorrent"**.

## Estrutura

```
Itorrent/
├─ src/
│  ├─ Itorrent.Core/        lógica e segurança, sem nenhuma dependência de Windows ou interface
│  │  ├─ Engine/            TorrentService, EngineFactory (perfis Normal e Turbo)
│  │  ├─ Policies/          PolicyEngine (parar de semear ao concluir)
│  │  ├─ Security/          LinkValidator, PathGuard, FileRiskChecker
│  │  ├─ Stats/             TorrentSnapshot (estado imutável entregue à interface a cada 1 s)
│  │  ├─ Storage/           SQLite: torrents e configurações
│  │  └─ Settings/          AppSettings
│  └─ Itorrent.Desktop/     interface WPF (MVVM)
│     ├─ Themes/Retro.xaml  tema Windows 3.1 (botões em relevo, menus, barras de rolagem…)
│     ├─ Assets/            ícone do app e ícones 16×16 em pixel-art
│     ├─ Views/ ViewModels/
│     └─ Platform/          instância única, bandeja, registro, impedir suspensão
├─ tests/Itorrent.Tests/    xUnit: PathGuard, magnet, fuzzing de .torrent, PolicyEngine, Turbo, persistência
├─ installer/Itorrent.iss   instalador Inno Setup
├─ icon/                    ícone do app (.ico e PNGs)
└─ Docs/                    documento de arquitetura e segurança
```

## Tecnologias

.NET 10 · WPF · [MonoTorrent](https://github.com/alanmcgovern/monotorrent) 3.0.2 (motor BitTorrent) · CommunityToolkit.Mvvm · Microsoft.Extensions.Hosting · SQLite (Microsoft.Data.Sqlite) · Serilog · H.NotifyIcon · xUnit.

Os builds usam `Nullable`, `TreatWarningsAsErrors`, os analisadores de segurança do .NET (`AnalysisMode=Recommended`) e `packages.lock.json`.

## Onde ficam os dados

| O quê | Onde |
|---|---|
| Banco (lista de torrents e configurações) | `%LocalAppData%\Itorrent\itorrent.db` |
| Dados de retomada e metadados | `%LocalAppData%\Itorrent\cache\` |
| Logs (rotação diária, 7 dias) | `%LocalAppData%\Itorrent\logs\` |
| Downloads (padrão) | `%UserProfile%\Downloads\Itorrent` |

## Aviso legal

Criar e usar um cliente BitTorrent é legal. Baixar ou compartilhar conteúdo protegido por direitos autorais sem autorização não é, e a responsabilidade é de quem usa. Todo peer do swarm vê o seu endereço IP.
