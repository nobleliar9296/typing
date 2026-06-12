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

$repoRoot = Get-FullPath (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "TypingTrainer.sln"
$appProjectPath = Join-Path $repoRoot "src\TypingTrainer.App\TypingTrainer.App.csproj"
$issPath = Join-Path $repoRoot "installer\TypingTrainer.iss"

$artifactsRoot = Get-FullPath (Join-Path $repoRoot "artifacts")
$publishRoot = Get-FullPath (Join-Path $artifactsRoot "publish")
$publishDir = Get-FullPath (Join-Path $publishRoot "TypingTrainer.App")
$installerDir = Get-FullPath (Join-Path $artifactsRoot "installer")
$appExePath = Join-Path $publishDir "TypingTrainer.App.exe"
$installerPath = Join-Path $installerDir "TypingTrainerSetup.exe"

foreach ($requiredPath in @($solutionPath, $appProjectPath, $issPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required file was not found: $requiredPath"
    }
}

Assert-PathUnder -Path $publishRoot -ParentPath $artifactsRoot -Description "Publish artifact"
Assert-PathUnder -Path $installerDir -ParentPath $artifactsRoot -Description "Installer artifact"

Write-Host "Repo root: $repoRoot"

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
$isccArgs = @(
    $publishDefine,
    $installerOutputDefine,
    $issPath
)
Invoke-NativeCommand -FilePath $isccPath -Arguments $isccArgs -FailureMessage "Inno Setup compiler failed."

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer compilation completed, but the expected setup file was not found: $installerPath"
}

Write-Host ""
Write-Host "Installer created:"
Write-Host $installerPath
