; Instalador do Itorrent (Inno Setup 6).
; Instala por usuário em %LocalAppData%\Programs\Itorrent, sem pedir administrador,
; e registra magnet: e .torrent em HKCU (nunca HKLM).
;
; Antes de compilar:
;   dotnet publish src/Itorrent.Desktop -c Release -r win-x64 --self-contained ^
;     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64

#define AppName "Itorrent"
#define AppVersion "1.0.0"
#define AppExe "Itorrent.exe"

[Setup]
AppId={{6B0E8C1A-4E2B-4C55-9F2A-1D7A3C9E5B10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Itorrent
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=
DisableProgramGroupPage=yes
OutputDir=..\publish
OutputBaseFilename=Itorrent-Setup-{#AppVersion}
SetupIconFile=..\icon\itorrent.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=classic

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked
Name: "associate"; Description: "Abrir links magnet e arquivos .torrent com o Itorrent"
Name: "startup"; Description: "Iniciar com o Windows (na bandeja)"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Protocolo magnet:
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueData: "URL:Magnet Protocol"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExe}"",0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: associate
; Arquivos .torrent
Root: HKCU; Subkey: "Software\Classes\Itorrent.Torrent"; ValueType: string; ValueData: "Arquivo Torrent"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Itorrent.Torrent\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExe}"",0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Itorrent.Torrent\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.torrent"; ValueType: string; ValueData: "Itorrent.Torrent"; Flags: uninsdeletevalue; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.torrent"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-bittorrent"; Tasks: associate
; Iniciar com o Windows
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Itorrent"; ValueData: """{app}\{#AppExe}"" --tray"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o Itorrent"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "KillItorrent"
