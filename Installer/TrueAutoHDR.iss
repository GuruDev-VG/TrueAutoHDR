#define MyAppName "TrueAuto HDR"
#define MyAppVersion "1.3.2"
#define MyAppPublisher "VG Prod."
#define MyAppExeName "TrueAutoHDR.exe"

[Setup]
AppId={{B8AD44C0-8210-4E39-A18D-27123F4625CE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\TrueAutoHDR
DefaultGroupName=TrueAuto HDR
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\release\Installer
OutputBaseFilename=TrueAutoHDR-1.3.2-Setup
SetupIconFile=..\Assets\AutoHDR.ico
UninstallDisplayIcon={app}\TrueAutoHDR.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
AppMutex=Local\TrueAutoHDR_SingleInstance
ChangesAssociations=no
VersionInfoVersion=1.3.2.0
VersionInfoDescription=TrueAuto HDR Setup
VersionInfoProductName=TrueAuto HDR
VersionInfoProductVersion=1.3.2

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\release\InstallerInput\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TrueAuto HDR"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall TrueAuto HDR"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TrueAuto HDR"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch TrueAuto HDR"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Always remove auto-start entries owned by TrueAuto HDR. }
    RegDeleteValue(HKCU, RunKey, 'TrueAutoHDR');
    RegDeleteValue(HKCU, RunKey, 'AutoHDR');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    DataPath := ExpandConstant('{localappdata}\TrueAutoHDR');
    if DirExists(DataPath) then
    begin
      if MsgBox(
        'Remove your TrueAuto HDR settings, logs, custom games, and local HDR database too?' + #13#10 + #13#10 +
        'Choose No if you may reinstall later.',
        mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataPath, True, True, True);
      end;
    end;
  end;
end;
