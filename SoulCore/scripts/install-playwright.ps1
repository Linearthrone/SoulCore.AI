# Install Playwright Chromium for SoulCore Host (BED-195 / OPS-198).
# Soft-fail friendly: ALLSTART calls this and continues if it fails.
#
# Usage (Windows PowerShell 5.1 is enough — PowerShell 7 / pwsh NOT required):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\SoulCore\scripts\install-playwright.ps1
#   .\SoulCore\scripts\install-playwright.ps1
#   .\SoulCore\scripts\install-playwright.ps1 -VerifyOnly
#
# Chromium binaries land in:  %LOCALAPPDATA%\ms-playwright\
# Victoria's profile (cookies/tabs) is separate:  %LOCALAPPDATA%\SoulCore\victoria-browser\

param(
    [switch]$SkipBrowserDownload,
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$HostProj = Join-Path $RepoRoot "SoulCore\SoulCore.Host\SoulCore.Host.csproj"
$InferenceProj = Join-Path $RepoRoot "SoulCore\SoulCore.Inference\SoulCore.Inference.csproj"
$MsPlaywrightDir = Join-Path $env:LOCALAPPDATA "ms-playwright"
$VictoriaProfile = Join-Path $env:LOCALAPPDATA "SoulCore\victoria-browser"

function Test-ChromiumInstalled {
    if (-not (Test-Path -LiteralPath $MsPlaywrightDir)) { return $false }
    $chrome = Get-ChildItem -Path $MsPlaywrightDir -Recurse -Filter "chrome.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'chromium' } |
        Select-Object -First 1
    return $null -ne $chrome
}

function Show-ChromiumStatus {
    Write-Host "Browser cache: $MsPlaywrightDir"
    Write-Host "Victoria profile (not the Chromium binary): $VictoriaProfile"
    if (Test-ChromiumInstalled) {
        $chrome = Get-ChildItem -Path $MsPlaywrightDir -Recurse -Filter "chrome.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match 'chromium' } |
            Select-Object -First 1
        Write-Host "FOUND: $($chrome.FullName)" -ForegroundColor Green
        return $true
    }
    Write-Host "MISSING: no chromium chrome.exe under $MsPlaywrightDir" -ForegroundColor Yellow
    return $false
}

if ($VerifyOnly) {
    Write-Host "=== install-playwright: verify only ==="
    if (Show-ChromiumStatus) { exit 0 }
    Write-Warning "Chromium not installed. Re-run without -VerifyOnly."
    exit 1
}

# Fast path: already installed — do not rebuild or re-download (keeps ALLSTART under timeout).
if (Test-ChromiumInstalled) {
    Write-Host "=== install-playwright: Chromium already present ==="
    [void](Show-ChromiumStatus)
    Write-Host "Playwright Chromium ready." -ForegroundColor Green
    exit 0
}

Write-Host "=== install-playwright: build Host (Release) so playwright.ps1 matches runtime ==="
dotnet build $HostProj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Warning "dotnet build Host failed (exit $LASTEXITCODE) — trying Inference restore/build"
    dotnet restore $InferenceProj
    dotnet build $InferenceProj -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet build failed (exit $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# Prefer the Host Release CLI (same bits the running Host uses). Never pick a random
# first hit under SoulCore/ — that used to grab Debug/Hermes copies unpredictably.
$candidates = @(
    (Join-Path $RepoRoot "SoulCore\SoulCore.Host\bin\Release\net8.0\playwright.ps1"),
    (Join-Path $RepoRoot "SoulCore\SoulCore.Inference\bin\Release\net8.0\playwright.ps1"),
    (Join-Path $RepoRoot "SoulCore\SoulCore.Host\bin\Debug\net8.0\playwright.ps1")
)
$playwrightCli = $null
foreach ($c in $candidates) {
    if (Test-Path -LiteralPath $c) {
        $playwrightCli = $c
        break
    }
}

if (-not $playwrightCli) {
    $nugetRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.playwright"
    $nuget = Get-ChildItem -Path $nugetRoot -Recurse -Filter "playwright.ps1" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -match '[\\/]1\.49\.0[\\/]' -or
            $_.DirectoryName -match '[\\/]build([\\/]|$)'
        } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($nuget) { $playwrightCli = $nuget.FullName }
}

if (-not $playwrightCli) {
    Write-Warning "playwright.ps1 not found. Manual:"
    Write-Host "  dotnet build .\SoulCore\SoulCore.Host\SoulCore.Host.csproj -c Release"
    Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\SoulCore\SoulCore.Host\bin\Release\net8.0\playwright.ps1 install chromium"
    exit 1
}

Write-Host "Using CLI: $playwrightCli"
if ($SkipBrowserDownload) {
    Write-Host "SkipBrowserDownload set — package/build only."
    exit 0
}

Write-Host "=== install-playwright: download Chromium (this can take several minutes) ==="
Write-Host "Target cache: $MsPlaywrightDir"
Write-Host "Do NOT cancel — ALLSTART used to kill this at 3 minutes; leave it running."
# Invoke via powershell.exe so Windows PowerShell 5.1 captures a real process exit code
# (nested `exit` from `& script.ps1` is unreliable on PS 5.1).
# Pass install args AFTER -File so they land in $args for Microsoft.Playwright.Program.Main.
$install = Start-Process -FilePath "powershell.exe" `
    -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $playwrightCli, "install", "chromium") `
    -WorkingDirectory (Split-Path -Parent $playwrightCli) `
    -Wait -PassThru -NoNewWindow
$exit = $install.ExitCode
if ($null -eq $exit) { $exit = 0 }
if ($exit -ne 0) {
    Write-Warning "playwright install chromium exited $exit"
    [void](Show-ChromiumStatus)
    exit $exit
}

if (-not (Show-ChromiumStatus)) {
    Write-Warning "Install reported success but chrome.exe was not found under $MsPlaywrightDir"
    Write-Host "Check PLAYWRIGHT_BROWSERS_PATH if set: '$env:PLAYWRIGHT_BROWSERS_PATH'"
    exit 2
}

Write-Host "Playwright Chromium ready." -ForegroundColor Green
Write-Host "Next: .\ALLSTART.ps1 -RestartHost   then ask Victoria to open a site (or browser_health)."
exit 0
