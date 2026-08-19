# Install Playwright Chromium for SoulCore Host (BED-195 / OPS-198).
# Soft-fail friendly: ALLSTART calls this and continues if it fails.
#
# Usage:
#   pwsh SoulCore/scripts/install-playwright.ps1
#   pwsh SoulCore/scripts/install-playwright.ps1 -SkipBrowserDownload  # restore only

param(
    [switch]$SkipBrowserDownload
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$InferenceProj = Join-Path $RepoRoot "SoulCore\SoulCore.Inference\SoulCore.Inference.csproj"

Write-Host "=== install-playwright: restore Microsoft.Playwright ==="
dotnet restore $InferenceProj
if ($LASTEXITCODE -ne 0) {
    Write-Warning "dotnet restore failed (exit $LASTEXITCODE)"
    exit $LASTEXITCODE
}

# Prefer the Playwright CLI shipped with the NuGet package.
$playwrightDll = Get-ChildItem -Path (Join-Path $RepoRoot "SoulCore") -Recurse -Filter "playwright.ps1" -ErrorAction SilentlyContinue |
    Select-Object -First 1

if (-not $playwrightDll) {
    # Fallback: install via `dotnet tool` pattern used by Playwright docs.
    Write-Host "Running: dotnet build + microsoft.playwright.cli install chromium"
    dotnet build $InferenceProj -c Release --no-restore | Out-Null
    $pwshCli = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\microsoft.playwright" -Recurse -Filter "playwright.ps1" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $pwshCli) {
        Write-Warning "playwright.ps1 not found under NuGet packages. Manual: pwsh -c \"dotnet build; & `$HOME/.nuget/packages/microsoft.playwright/*/lib/net*/playwright.ps1 install chromium\""
        exit 1
    }
    $playwrightDll = $pwshCli
}

Write-Host "Using $($playwrightDll.FullName)"
if ($SkipBrowserDownload) {
    Write-Host "SkipBrowserDownload set — package restore only."
    exit 0
}

& $playwrightDll.FullName install chromium
$exit = $LASTEXITCODE
if ($exit -ne 0) {
    Write-Warning "playwright install chromium exited $exit — Host browser_* will fail until Chromium is installed."
    exit $exit
}

Write-Host "Playwright Chromium ready. Profile default: %LOCALAPPDATA%\SoulCore\victoria-browser"
exit 0
