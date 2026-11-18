#define AppVersion "1.0.0-beta.1"
#ifndef PayloadDir
  #define PayloadDir "..\..\build\packaging\desktop"
#endif

[Setup]
AppId={{FA5E2F39-88D5-43CD-95A9-2B81828E58D1}
AppName=WT MultiCam
AppVersion={#AppVersion}
AppPublisher=WilkensTech
AppVerName=WT MultiCam {#AppVersion}
VersionInfoVersion=1.0.0.0
DefaultDirName={localappdata}\Programs\WilkensTech\WTMultiCam
DefaultGroupName=WilkensTech\WT MultiCam
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\Windows
OutputBaseFilename=WTMultiCam-Setup-1.0.0-beta.1-win-x64
SetupIconFile=Assets\WTMultiCam.ico
UninstallDisplayIcon={app}\MovilCamara.exe
WizardStyle=modern
WizardSizePercent=120
WizardImageFile=Assets\Wizard.bmp
WizardSmallImageFile=Assets\WizardSmall.bmp
WizardImageStretch=yes
DisableWelcomePage=no
DisableProgramGroupPage=yes
Compression=lzma2/fast
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
AppMutex=Local\WTMultiCam.Desktop
InfoBeforeFile=LEEME.txt
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
spanish.WelcomeLabel1=Todo listo para conectar tus cámaras
spanish.WelcomeLabel2=Instala el receptor de WT MultiCam en este equipo.%n%nCámaras independientes en OBS%nConexión por USB y Wi-Fi%nAudio y controles desde tu PC%n%n.NET incluido: no necesitas instalarlo por separado.%n%nWindows 11 · 64 bits
spanish.FinishedHeadingLabel=Tu estudio está listo
spanish.FinishedLabel=WT MultiCam está instalado.%n%nAbre el receptor, conecta tu teléfono y empieza a transmitir.%n%nLa primera activación de una cámara virtual puede solicitar autorización de Windows.

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "LEEME.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\WT MultiCam"; Filename: "{app}\MovilCamara.exe"
Name: "{group}\Desinstalar WT MultiCam"; Filename: "{uninstallexe}"
Name: "{autodesktop}\WT MultiCam"; Filename: "{app}\MovilCamara.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\MovilCamara.exe"; Description: "Abrir WT MultiCam"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
begin
  Result := True;
  if CheckForMutexes('Local\WTMultiCam.Desktop') then begin
    MsgBox('Cierra WT MultiCam antes de desinstalarlo.', mbInformation, MB_OK);
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
begin
  if CurUninstallStep <> usUninstall then Exit;
  if FileExists(ExpandConstant('{app}\MovilCamara.exe')) then begin
    if not Exec(ExpandConstant('{app}\MovilCamara.exe'), '--prepare-uninstall', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
      MsgBox('No se pudo preparar la desinstalación. Vuelve a intentarlo con WT MultiCam y OBS cerrados.', mbError, MB_OK);
      Abort;
    end;
    if ExitCode <> 0 then begin
      MsgBox('No se retiraron todas las cámaras virtuales. Cierra OBS y otras aplicaciones de cámara y vuelve a intentarlo.', mbError, MB_OK);
      Abort;
    end;
  end;
end;
