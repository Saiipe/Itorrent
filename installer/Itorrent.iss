; Instalador do Itorrent (Inno Setup 6).
; Instala por usuário em %LocalAppData%\Programs\Itorrent, sem pedir administrador,
; e registra magnet: e .torrent em HKCU (nunca HKLM).
;
; Antes de compilar:
;   dotnet publish src/Itorrent.Desktop -c Release -r win-x64 --self-contained ^
;     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64

#define AppName "Itorrent"
#define AppVersion "1.1.0"
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
DisableWelcomePage=no
; Imagens do assistente com o ícone do Itorrent (geradas por make-wizard-images.ps1).
; Vários tamanhos: o Inno Setup escolhe o certo para a escala da tela (100% a 250%).
WizardSmallImageFile=images\small-55.bmp,images\small-64.bmp,images\small-69.bmp,images\small-83.bmp,images\small-92.bmp,images\small-110.bmp,images\small-119.bmp,images\small-138.bmp
WizardImageFile=images\large-164.bmp,images\large-192.bmp,images\large-246.bmp,images\large-273.bmp,images\large-328.bmp,images\large-355.bmp,images\large-410.bmp
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Instalador do Itorrent

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

[Code]
// Na desinstalação, remove tudo o que o Itorrent registrou no Windows, inclusive o que foi
// ligado depois pelo próprio app (Opções), e não só o que o instalador criou.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
  DataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Itorrent');

    // Só remove o protocolo magnet se ele ainda aponta para o Itorrent (outro app pode ter assumido).
    if RegQueryStringValue(HKCU, 'Software\Classes\magnet\shell\open\command', '', Value) and
       (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Value)) > 0) then
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\magnet');

    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\Itorrent.Torrent');
    if RegQueryStringValue(HKCU, 'Software\Classes\.torrent', '', Value) and (Value = 'Itorrent.Torrent') then
      RegDeleteValue(HKCU, 'Software\Classes\.torrent', '');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\Itorrent');
    if DirExists(DataDir) and not UninstallSilent and
       (MsgBox('Apagar também as configurações, a lista de torrents e os logs do Itorrent?' + #13#10#13#10 +
               'Os arquivos que você baixou NÃO serão apagados.',
               mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(DataDir, True, True, True);
  end;
end;
