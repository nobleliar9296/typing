$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$installerDir = Join-Path $repoRoot "installer"
$issPath = Join-Path $installerDir "TypingTrainer.iss"
$appProjectPath = Join-Path $repoRoot "src\TypingTrainer.App\TypingTrainer.App.csproj"

New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

function Get-ProjectProperty {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$ProjectXml,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$DefaultValue
    )

    foreach ($propertyGroup in $ProjectXml.Project.PropertyGroup) {
        $value = $propertyGroup.$Name

        if ($value) {
            $text = ([string]$value).Trim()

            if ($text.Length -gt 0) {
                return $text
            }
        }
    }

    return $DefaultValue
}

function ConvertTo-InnoDefineValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return $Value.Replace('"', '""')
}

if (-not (Test-Path -LiteralPath $appProjectPath)) {
    throw "Required file was not found: $appProjectPath"
}

$appProjectXml = [xml](Get-Content -LiteralPath $appProjectPath -Raw)
$appName = ConvertTo-InnoDefineValue (Get-ProjectProperty -ProjectXml $appProjectXml -Name "Product" -DefaultValue "Typing Trainer")
$appPublisher = ConvertTo-InnoDefineValue (Get-ProjectProperty -ProjectXml $appProjectXml -Name "Company" -DefaultValue "Gundeep Sidhu")
$appVersion = ConvertTo-InnoDefineValue (Get-ProjectProperty -ProjectXml $appProjectXml -Name "Version" -DefaultValue "1.0.0")
$appVersionQuad = ConvertTo-InnoDefineValue (Get-ProjectProperty -ProjectXml $appProjectXml -Name "FileVersion" -DefaultValue "1.0.0.0")
$appPublisherUrl = ConvertTo-InnoDefineValue (Get-ProjectProperty -ProjectXml $appProjectXml -Name "PackageProjectUrl" -DefaultValue "https://gundeepsidhu.dev")
$appCopyright = ConvertTo-InnoDefineValue (Get-ProjectProperty -ProjectXml $appProjectXml -Name "Copyright" -DefaultValue "Copyright (c) Gundeep Sidhu")

$issContent = @"
#ifndef AppName
  #define AppName "$appName"
#endif

#ifndef AppExeName
  #define AppExeName "TypingTrainer.App.exe"
#endif

#ifndef AppPublisher
  #define AppPublisher "$appPublisher"
#endif

#ifndef AppVersion
  #define AppVersion "$appVersion"
#endif

#ifndef AppVersionQuad
  #define AppVersionQuad "$appVersionQuad"
#endif

#ifndef AppPublisherUrl
  #define AppPublisherUrl "$appPublisherUrl"
#endif

#ifndef AppCopyright
  #define AppCopyright "$appCopyright"
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
"@

Set-Content -LiteralPath $issPath -Value $issContent -Encoding ascii

Write-Host "Generated installer script:"
Write-Host $issPath
