param(
    [string]$SigningCertificateThumbprint = $env:TYPINGTRAINER_SIGNING_THUMBPRINT,
    [string]$SigningPfxPath = $env:TYPINGTRAINER_SIGNING_PFX,
    [string]$SigningPfxPasswordEnvironmentVariable = "TYPINGTRAINER_SIGNING_PFX_PASSWORD",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$SkipSigning
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-PathUnder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $fullPath = (Get-FullPath $Path).TrimEnd('\')
    $fullParentPath = (Get-FullPath $ParentPath).TrimEnd('\')

    if ($fullPath -eq $fullParentPath) {
        return
    }

    if (-not $fullPath.StartsWith($fullParentPath + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description path '$fullPath' is outside '$fullParentPath'."
    }
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE."
    }
}

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

function Get-SignToolPath {
    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue

    if ($command) {
        return $command.Source
    }

    $windowsKitsBin = "C:\Program Files (x86)\Windows Kits\10\bin"

    if (-not (Test-Path -LiteralPath $windowsKitsBin)) {
        return $null
    }

    $candidate = Get-ChildItem -LiteralPath $windowsKitsBin -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if ($candidate) {
        return $candidate.FullName
    }

    return $null
}

function Invoke-CodeSign {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileToSign,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not $signingRequested) {
        return
    }

    if (-not (Test-Path -LiteralPath $FileToSign)) {
        throw "Cannot sign missing file: $FileToSign"
    }

    $signToolPath = Get-SignToolPath

    if (-not $signToolPath) {
        throw "Code signing was requested, but signtool.exe was not found. Install the Windows SDK or put signtool.exe on PATH."
    }

    Write-Host "Signing $Description..."

    $signArgs = @(
        "sign",
        "/fd",
        "SHA256",
        "/tr",
        $TimestampUrl,
        "/td",
        "SHA256",
        "/d",
        "Typing Trainer",
        "/du",
        $appPublisherUrl
    )

    if ($SigningCertificateThumbprint) {
        $signArgs += @("/sha1", $SigningCertificateThumbprint)
    }
    elseif ($SigningPfxPath) {
        if (-not (Test-Path -LiteralPath $SigningPfxPath)) {
            throw "Signing PFX file was not found: $SigningPfxPath"
        }

        $signArgs += @("/f", $SigningPfxPath)

        $pfxPassword = [Environment]::GetEnvironmentVariable($SigningPfxPasswordEnvironmentVariable)

        if ($pfxPassword) {
            $signArgs += @("/p", $pfxPassword)
        }
        else {
            Write-Host "No PFX password was found in '$SigningPfxPasswordEnvironmentVariable'; trying the PFX without a password."
        }
    }

    $signArgs += $FileToSign

    Invoke-NativeCommand -FilePath $signToolPath -Arguments $signArgs -FailureMessage "Code signing failed for $FileToSign."
}

$repoRoot = Get-FullPath (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "TypingTrainer.sln"
$appProjectPath = Join-Path $repoRoot "src\TypingTrainer.App\TypingTrainer.App.csproj"
$issPath = Join-Path $repoRoot "installer\TypingTrainer.iss"
$appIconPath = Join-Path $repoRoot "src\TypingTrainer.App\Assets\AppIcon.ico"

$artifactsRoot = Get-FullPath (Join-Path $repoRoot "artifacts")
$publishRoot = Get-FullPath (Join-Path $artifactsRoot "publish")
$publishDir = Get-FullPath (Join-Path $publishRoot "TypingTrainer.App")
$installerDir = Get-FullPath (Join-Path $artifactsRoot "installer")
$appExePath = Join-Path $publishDir "TypingTrainer.App.exe"
$installerPath = Join-Path $installerDir "TypingTrainerSetup.exe"

foreach ($requiredPath in @($solutionPath, $appProjectPath, $issPath, $appIconPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required file was not found: $requiredPath"
    }
}

$appProjectXml = [xml](Get-Content -LiteralPath $appProjectPath -Raw)
$appVersion = Get-ProjectProperty -ProjectXml $appProjectXml -Name "Version" -DefaultValue "1.0.0"
$appVersionQuad = Get-ProjectProperty -ProjectXml $appProjectXml -Name "FileVersion" -DefaultValue "1.0.0.0"
$appPublisher = Get-ProjectProperty -ProjectXml $appProjectXml -Name "Company" -DefaultValue "Gundeep Sidhu"
$appPublisherUrl = Get-ProjectProperty -ProjectXml $appProjectXml -Name "PackageProjectUrl" -DefaultValue "https://gundeepsidhu.dev"

$signingRequested = -not $SkipSigning -and (
    -not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or
    -not [string]::IsNullOrWhiteSpace($SigningPfxPath)
)

Assert-PathUnder -Path $publishRoot -ParentPath $artifactsRoot -Description "Publish artifact"
Assert-PathUnder -Path $installerDir -ParentPath $artifactsRoot -Description "Installer artifact"

Write-Host "Repo root: $repoRoot"
Write-Host "App version: $appVersion"
Write-Host "Publisher: $appPublisher"
Write-Host "Publisher URL: $appPublisherUrl"

if ($signingRequested) {
    Write-Host "Code signing: enabled"
}
else {
    Write-Host "Code signing: skipped. Set TYPINGTRAINER_SIGNING_THUMBPRINT or TYPINGTRAINER_SIGNING_PFX to enable it."
}

foreach ($artifactDir in @($publishRoot, $installerDir)) {
    if (Test-Path -LiteralPath $artifactDir) {
        Write-Host "Cleaning packaging artifact folder: $artifactDir"
        Remove-Item -LiteralPath $artifactDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing TypingTrainer.App to: $publishDir"
$publishArgs = @(
    "publish",
    $appProjectPath,
    "-c",
    "Release",
    "-p:Platform=x64",
    "-r",
    "win-x64",
    "--self-contained",
    "true",
    "-p:WindowsPackageType=None",
    "-p:WindowsAppSDKSelfContained=true",
    "-p:PublishSingleFile=false",
    "-o",
    $publishDir
)
Invoke-NativeCommand -FilePath "dotnet" -Arguments $publishArgs -FailureMessage "dotnet publish failed."

if (-not (Test-Path -LiteralPath $appExePath)) {
    throw "Publish completed, but the expected executable was not found: $appExePath"
}

Write-Host "Verified published executable: $appExePath"
Invoke-CodeSign -FileToSign $appExePath -Description "app executable"

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $isccPath) {
    Write-Host ""
    Write-Host "Inno Setup 6 compiler was not found." -ForegroundColor Red
    Write-Host "Install Inno Setup 6, then rerun:"
    Write-Host "  powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1"
    Write-Host ""
    Write-Host "Expected ISCC.exe in one of these locations:"
    foreach ($candidate in $isccCandidates) {
        Write-Host "  $candidate"
    }
    exit 1
}

Write-Host "Using Inno Setup compiler: $isccPath"

$publishDefine = '/DPublishDir="' + $publishDir + '"'
$installerOutputDefine = '/DInstallerOutputDir="' + $installerDir + '"'
$appIconDefine = '/DAppIconFile="' + $appIconPath + '"'
$appVersionDefine = '/DAppVersion="' + $appVersion + '"'
$appVersionQuadDefine = '/DAppVersionQuad="' + $appVersionQuad + '"'
$appPublisherDefine = '/DAppPublisher="' + $appPublisher + '"'
$appPublisherUrlDefine = '/DAppPublisherUrl="' + $appPublisherUrl + '"'
$isccArgs = @(
    $publishDefine,
    $installerOutputDefine,
    $appIconDefine,
    $appVersionDefine,
    $appVersionQuadDefine,
    $appPublisherDefine,
    $appPublisherUrlDefine,
    $issPath
)
Invoke-NativeCommand -FilePath $isccPath -Arguments $isccArgs -FailureMessage "Inno Setup compiler failed."

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer compilation completed, but the expected setup file was not found: $installerPath"
}

Invoke-CodeSign -FileToSign $installerPath -Description "installer"

Write-Host ""
Write-Host "Installer created:"
Write-Host $installerPath
