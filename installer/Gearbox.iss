; Instalador do Gearbox (Inno Setup 6.3+).
; Não compile direto: use installer\build-installer.ps1, que publica o app e passa os parâmetros.
;
;   ISCC.exe /DSourceDir=<pasta publish> /DArch=x64|arm64 /DOutputDir=<saída> Gearbox.iss

#ifndef SourceDir
  #error Passe /DSourceDir=<pasta gerada pelo dotnet publish>
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define AppName "Gearbox"
#define AppExe "Gearbox.exe"
#define AppVersion GetVersionNumbersString(AddBackslash(SourceDir) + AppExe)
#if AppVersion == ""
  #error Gearbox.exe não encontrado em SourceDir
#endif

[Setup]
; Nunca mude o AppId: é ele que liga atualizações e desinstalação à instalação existente.
AppId={{E4A7C2B1-5D3F-4C8E-9A61-2F0B7D9C3E54}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Gearbox
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}

; Instalação por usuário, sem administrador: %LocalAppData%\Programs\Gearbox.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableReadyPage=yes

#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.19041

OutputDir={#OutputDir}
OutputBaseFilename=Gearbox-{#AppVersion}-{#Arch}-setup
SetupIconFile=..\src\Gearbox\Assets\Gearbox.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes

; O Gearbox é encerrado pelo próprio instalador com "--exit" (ver [Code]), que restaura as janelas
; pseudo-maximizadas. O Restart Manager mataria o processo sem essa limpeza.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
brazilianportuguese.StartupTask=Iniciar o Gearbox com o Windows
brazilianportuguese.LaunchAfterInstall=Iniciar o Gearbox agora
brazilianportuguese.CloseFailed=Não foi possível encerrar o Gearbox. Saia dele pelo menu da bandeja (Sair) e tente de novo.
brazilianportuguese.RemoveUserData=Remover também as configurações e os logs do Gearbox?

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Mesmo valor que o Gearbox grava (RegistryStartupService), então o toggle da página Geral reflete a escolha.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Gearbox"; ValueData: """{app}\{#AppExe}"" --startup"; Tasks: startup

[InstallDelete]
; Arquivos da versão anterior ao novo nome (Darwin): o mesmo AppId atualiza a instalação na mesma pasta.
Type: files; Name: "{app}\Darwin*"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  { Nome anterior do app: usado só para migrar instalações antigas. }
  LegacyName = 'Darwin';

{ Há algum processo rodando a partir deste caminho? (WMI, não precisa de administrador) }
function IsRunningFrom(const ExePath: String): Boolean;
var
  Locator, Service, Processes: Variant;
  Query: String;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    { WQL exige as barras escapadas: C:\\Users\\... }
    Query := 'SELECT ProcessId FROM Win32_Process WHERE Name = ''' + ExtractFileName(ExePath) +
      ''' AND ExecutablePath = ''' + ExePath + '''';
    StringChangeEx(Query, '\', '\\', True);
    Processes := Service.ExecQuery(Query);
    Result := Processes.Count > 0;
  except
    Log('Falha ao consultar processos via WMI: ' + GetExceptionMessage);
  end;
end;

{ Pede para o app instalado sair pelo caminho normal (restaura as janelas) e espera até 10 s. }
function CloseApp(const AppDir, ExeName: String): Boolean;
var
  ExePath: String;
  ResultCode, I: Integer;
begin
  Result := True;
  ExePath := AddBackslash(AppDir) + ExeName;
  if not FileExists(ExePath) or not IsRunningFrom(ExePath) then
    Exit;

  Log('Encerrando ' + ExeName + ' em execução');
  Exec(ExePath, '--exit', AppDir, SW_HIDE, ewWaitUntilTerminated, ResultCode);

  for I := 1 to 40 do
  begin
    if not IsRunningFrom(ExePath) then
      Exit;
    Sleep(250);
  end;

  Result := False;
end;

function CloseGearbox(const AppDir: String): Boolean;
begin
  Result := CloseApp(AppDir, '{#AppExe}');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  AppDir: String;
begin
  Result := '';
  AppDir := ExpandConstant('{app}');
  if not CloseGearbox(AppDir) or not CloseApp(AppDir, LegacyName + '.exe') then
    Result := CustomMessage('CloseFailed');
end;

{ Atualização a partir do Darwin: leva o "Iniciar com o Windows" e as configurações para o nome novo. }
procedure MigrateLegacy();
var
  Command, OldData, NewData: String;
begin
  if RegQueryStringValue(HKCU, RunKey, LegacyName, Command) and
     (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Command)) > 0) then
  begin
    RegDeleteValue(HKCU, RunKey, LegacyName);
    RegWriteStringValue(HKCU, RunKey, '{#AppName}', '"' + ExpandConstant('{app}\{#AppExe}') + '" --startup');
    Log('Iniciar com o Windows migrado de ' + LegacyName);
  end;

  OldData := ExpandConstant('{localappdata}\') + LegacyName;
  NewData := ExpandConstant('{localappdata}\{#AppName}');
  if DirExists(OldData) and not DirExists(NewData) then
  begin
    if RenameFile(OldData, NewData) then
      Log('Dados movidos de ' + OldData + ' para ' + NewData)
    else
      Log('Não foi possível mover ' + OldData + ' para ' + NewData);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    MigrateLegacy();
end;

function InitializeUninstall(): Boolean;
begin
  Result := CloseGearbox(ExpandConstant('{app}'));
  if not Result then
    MsgBox(CustomMessage('CloseFailed'), mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command, DataDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  { O usuário pode ter ligado "Iniciar com o Windows" pelo próprio app: remove se apontar para esta instalação. }
  if RegQueryStringValue(HKCU, RunKey, 'Gearbox', Command) and
     (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Command)) > 0) then
    RegDeleteValue(HKCU, RunKey, 'Gearbox');

  DataDir := ExpandConstant('{localappdata}\Gearbox');
  if DirExists(DataDir) and
     (SuppressibleMsgBox(CustomMessage('RemoveUserData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES) then
    DelTree(DataDir, True, True, True);
end;
