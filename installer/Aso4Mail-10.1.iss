; Aso4Mail 10.1 experimental installer, built from reviewed source.
; No PowerShell, no Windows Defender exclusions, no automatic Windows startup.
#define MyAppName "Aso4Mail"
#define MyAppVersion "10.1"
#define MyAppExe "Aso4Mail.exe"

[Setup]
AppId={{4F5B8A9E-3A2E-4503-91C9-C94D0EF80917}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName=Aso4Mail 10.1
AppPublisher=Aso4Tech
AppPublisherURL=https://aso4tech.com/
AppSupportURL=https://aso4tech.com/
DefaultDirName={autopf}\Aso4Tech\Aso4Mail
DefaultGroupName=Aso4Mail
UninstallDisplayIcon={app}\{#MyAppExe}
UsePreviousAppDir=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
WizardStyle=modern dark
Compression=lzma2
SolidCompression=yes
OutputDir=installer-output
OutputBaseFilename=Aso4Mail-Setup-10.1-UNVERIFIED
CloseApplications=yes

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Aso4Mail"; Filename: "{app}\{#MyAppExe}"; AppUserModelID: "com.aso4tech.aso4mail"
Name: "{autodesktop}\Aso4Mail"; Filename: "{app}\{#MyAppExe}"; AppUserModelID: "com.aso4tech.aso4mail"

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch Aso4Mail"; Flags: postinstall nowait skipifsilent
