; Instalador do Itorrent (Inno Setup 6).
; Instala por usuário em %LocalAppData%\Programs\Itorrent, sem pedir administrador,
; e registra magnet: e .torrent em HKCU (nunca HKLM).
;
; Antes de compilar:
;   dotnet publish src/Itorrent.Desktop -c Release -r win-x64 --self-contained ^
;     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64

#define AppName "Itorrent"
#define AppVersion "1.2.0"
#define AppExe "Itorrent.exe"
#define AppPublisher "Saipe"

[Setup]
AppId={{6B0E8C1A-4E2B-4C55-9F2A-1D7A3C9E5B10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
; Como aparece em Configurações > Aplicativos: nome limpo, editor e versão em campos próprios.
UninstallDisplayName={#AppName}
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
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoDescription=Itorrent Setup
; Pergunta o idioma no início (já sugerindo o do Windows).
ShowLanguageDialog=yes
LanguageDetectionMethod=uilanguage

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
; Idioma que o app usa na primeira abertura depois de instalar.
ptbr.AppLang=pt-BR
en.AppLang=en
ptbr.TaskAssociate=Abrir links magnet e arquivos .torrent com o Itorrent
en.TaskAssociate=Open magnet links and .torrent files with Itorrent
ptbr.TaskStartup=Iniciar com o Windows (na bandeja)
en.TaskStartup=Start with Windows (in the tray)
ptbr.TorrentFile=Arquivo Torrent
en.TorrentFile=Torrent file
ptbr.DeleteData=Apagar também as configurações, a lista de torrents e os logs do Itorrent?
en.DeleteData=Also delete Itorrent settings, torrent list and logs?
ptbr.DeleteDataNote=Os arquivos que você baixou NÃO serão apagados.
en.DeleteDataNote=The files you downloaded will NOT be deleted.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "associate"; Description: "{cm:TaskAssociate}"
Name: "startup"; Description: "{cm:TaskStartup}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Protocolo magnet:
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueData: "URL:Magnet Protocol"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExe}"",0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\magnet\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: associate
; Arquivos .torrent
Root: HKCU; Subkey: "Software\Classes\Itorrent.Torrent"; ValueType: string; ValueData: "{cm:TorrentFile}"; Flags: uninsdeletekey; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Itorrent.Torrent\DefaultIcon"; ValueType: string; ValueData: """{app}\{#AppExe}"",0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\Itorrent.Torrent\shell\open\command"; ValueType: string; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.torrent"; ValueType: string; ValueData: "Itorrent.Torrent"; Flags: uninsdeletevalue; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.torrent"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-bittorrent"; Tasks: associate
; Idioma escolhido no instalador (o app aplica na primeira abertura)
Root: HKCU; Subkey: "Software\Itorrent"; ValueType: string; ValueName: "Language"; ValueData: "{cm:AppLang}"; Flags: uninsdeletekey
; Iniciar com o Windows
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Itorrent"; ValueData: """{app}\{#AppExe}"" --tray"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

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
       (MsgBox(CustomMessage('DeleteData') + #13#10#13#10 + CustomMessage('DeleteDataNote'),
               mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(DataDir, True, True, True);
  end;
end;
