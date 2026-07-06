#ifndef AppName
  #define AppName "Typing Trainer"
#endif

#ifndef AppExeName
  #define AppExeName "TypingTrainer.App.exe"
#endif

#ifndef AppPublisher
  #define AppPublisher "Gundeep Sidhu"
#endif

#ifndef AppVersion
  #define AppVersion "1.0.9"
#endif

#ifndef AppVersionQuad
  #define AppVersionQuad "1.0.9.0"
#endif

#ifndef AppPublisherUrl
  #define AppPublisherUrl "https://gundeepsidhu.dev"
#endif

#ifndef AppCopyright
  #define AppCopyright "Copyright (c) Gundeep Sidhu"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\TypingTrainer.App"
#endif

#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\artifacts\installer"
#endif

#ifndef AppIconFile
  #define AppIconFile "..\src\TypingTrainer.App\Assets\AppIcon.ico"
#endif

[Setup]
AppId={{1DD7B52B-1D79-47F3-9D9F-F53A7E60B768}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppPublisherUrl}
AppSupportURL={#AppPublisherUrl}
AppUpdatesURL={#AppPublisherUrl}
AppCopyright={#AppCopyright}
DefaultDirName={autopf}\Typing Trainer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=TypingTrainerSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
PrivilegesRequired=admin
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile={#AppIconFile}
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright={#AppCopyright}
VersionInfoDescription={#AppName} installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersionQuad}
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Typing Trainer"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Typing Trainer"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
