; Instalador do Darwin (Inno Setup 6.3+).
; Não compile direto: use installer\build-installer.ps1, que publica o app e passa os parâmetros.
;
;   ISCC.exe /DSourceDir=<pasta publish> /DArch=x64|arm64 /DOutputDir=<saída> Darwin.iss

#ifndef SourceDir
  #error Passe /DSourceDir=<pasta gerada pelo dotnet publish>
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define AppName "Darwin"
#define AppExe "Darwin.exe"
#define AppVersion GetVersionNumbersString(AddBackslash(SourceDir) + AppExe)
#if AppVersion == ""
  #error Darwin.exe não encontrado em SourceDir
#endif

[Setup]
; Nunca mude o AppId: é ele que liga atualizações e desinstalação à instalação existente.
AppId={{E4A7C2B1-5D3F-4C8E-9A61-2F0B7D9C3E54}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Darwin
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}

; Instalação por usuário, sem administrador: %LocalAppData%\Programs\Darwin.
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
OutputBaseFilename=Darwin-{#AppVersion}-{#Arch}-setup
SetupIconFile=..\src\Darwin\Assets\Darwin.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes

; O Darwin é encerrado pelo próprio instalador com "--exit" (ver [Code]), que restaura as janelas
; pseudo-maximizadas. O Restart Manager mataria o processo sem essa limpeza.
CloseApplications=no
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
brazilianportuguese.StartupTask=Iniciar o Darwin com o Windows
brazilianportuguese.LaunchAfterInstall=Iniciar o Darwin agora
brazilianportuguese.CloseFailed=Não foi possível encerrar o Darwin. Saia dele pelo menu da bandeja (Sair) e tente de novo.
brazilianportuguese.RemoveUserData=Remover também as configurações e os logs do Darwin?

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Mesmo valor que o Darwin grava (RegistryStartupService), então o toggle da página Geral reflete a escolha.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Darwin"; ValueData: """{app}\{#AppExe}"" --startup"; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

{ Há algum Darwin rodando a partir deste caminho? (WMI, não precisa de administrador) }
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
    Query := 'SELECT ProcessId FROM Win32_Process WHERE Name = ''Darwin.exe'' AND ExecutablePath = ''' +
      ExePath + '''';
    StringChangeEx(Query, '\', '\\', True);
    Processes := Service.ExecQuery(Query);
    Result := Processes.Count > 0;
  except
    Log('Falha ao consultar processos via WMI: ' + GetExceptionMessage);
  end;
end;

{ Pede para o Darwin instalado sair pelo caminho normal (restaura as janelas) e espera até 10 s. }
function CloseDarwin(const AppDir: String): Boolean;
var
  ExePath: String;
  ResultCode, I: Integer;
begin
  Result := True;
  ExePath := AddBackslash(AppDir) + '{#AppExe}';
  if not FileExists(ExePath) or not IsRunningFrom(ExePath) then
    Exit;

  Log('Encerrando o Darwin em execução');
  Exec(ExePath, '--exit', AppDir, SW_HIDE, ewWaitUntilTerminated, ResultCode);

  for I := 1 to 40 do
  begin
    if not IsRunningFrom(ExePath) then
      Exit;
    Sleep(250);
  end;

  Result := False;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CloseDarwin(ExpandConstant('{app}')) then
    Result := CustomMessage('CloseFailed');
end;

function InitializeUninstall(): Boolean;
begin
  Result := CloseDarwin(ExpandConstant('{app}'));
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
  if RegQueryStringValue(HKCU, RunKey, 'Darwin', Command) and
     (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Command)) > 0) then
    RegDeleteValue(HKCU, RunKey, 'Darwin');

  DataDir := ExpandConstant('{localappdata}\Darwin');
  if DirExists(DataDir) and
     (SuppressibleMsgBox(CustomMessage('RemoveUserData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES) then
    DelTree(DataDir, True, True, True);
end;
