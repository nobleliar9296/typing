param(
    [string]$SigningCertificateThumbprint = $env:TYPINGTRAINER_SIGNING_THUMBPRINT,
    [string]$SigningPfxPath = $env:TYPINGTRAINER_SIGNING_PFX,
    [string]$SigningPfxPasswordEnvironmentVariable = "TYPINGTRAINER_SIGNING_PFX_PASSWORD",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$SkipSigning,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repoRoot "TypingTrainer.sln"
$iconScriptPath = Join-Path $repoRoot "scripts\generate-app-icons.ps1"
$installerScriptPath = Join-Path $repoRoot "scripts\build-installer.ps1"
$installerPath = Join-Path $repoRoot "artifacts\installer\TypingTrainerSetup.exe"

foreach ($requiredPath in @($solutionPath, $iconScriptPath, $installerScriptPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required file was not found: $requiredPath"
    }
}

Write-Host "Repo root: $repoRoot"
Write-Host "Regenerating app icons..."
& $iconScriptPath

Write-Host "Building solution..."
Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
    "build",
    $solutionPath,
    "-c",
    "Release",
    "-p:Platform=x64"
) -FailureMessage "Release build failed."

if ($SkipTests) {
    Write-Host "Tests skipped by request."
}
else {
    Write-Host "Running tests..."
    Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
        "test",
        $solutionPath,
        "-c",
        "Release",
        "-p:Platform=x64",
        "--no-build"
    ) -FailureMessage "Tests failed."
}

$installerArgs = @{}

if ($SigningCertificateThumbprint) {
    $installerArgs["SigningCertificateThumbprint"] = $SigningCertificateThumbprint
}

if ($SigningPfxPath) {
    $installerArgs["SigningPfxPath"] = $SigningPfxPath
}

if ($SigningPfxPasswordEnvironmentVariable) {
    $installerArgs["SigningPfxPasswordEnvironmentVariable"] = $SigningPfxPasswordEnvironmentVariable
}

if ($TimestampUrl) {
    $installerArgs["TimestampUrl"] = $TimestampUrl
}

if ($SkipSigning) {
    $installerArgs["SkipSigning"] = $true
}

Write-Host "Building installer..."
& $installerScriptPath @installerArgs

if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Release completed, but the installer was not found: $installerPath"
}

Write-Host ""
Write-Host "Release installer:"
Write-Host $installerPath
