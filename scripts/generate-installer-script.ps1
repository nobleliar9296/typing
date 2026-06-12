$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$installerDir = Join-Path $repoRoot "installer"
$issPath = Join-Path $installerDir "TypingTrainer.iss"

New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

$issContent = @'
#define AppName "Typing Trainer"
#define AppExeName "TypingTrainer.App.exe"
#define AppPublisher "Typing Trainer"
#define AppVersion "1.0.0"

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
'@

Set-Content -LiteralPath $issPath -Value $issContent -Encoding ascii

Write-Host "Generated installer script:"
Write-Host $issPath
