# Install Playwright Chromium for SoulCore Host (BED-195 / OPS-198).
# Soft-fail friendly: ALLSTART only verifies; Kurt runs this once for the download.
#
# Usage (Windows PowerShell 5.1 is enough - PowerShell 7 / pwsh NOT required):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\SoulCore\scripts\install-playwright.ps1
#   .\SoulCore\scripts\install-playwright.ps1
#   .\SoulCore\scripts\install-playwright.ps1 -VerifyOnly
#
# Host uses Microsoft.Playwright 1.49.0 -> Chromium revision 1148.
# Binaries:  %LOCALAPPDATA%\ms-playwright\chromium-1148\chrome-win\chrome.exe
# Profile:   %LOCALAPPDATA%\SoulCore\victoria-browser\   (cookies/tabs - NOT the browser binary)

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
# Must match SoulCore.Inference Microsoft.Playwright package browsers.json (1.49.0 = 1148).
$ChromiumRevision = "1148"

function Get-ExpectedChromePaths {
    $roots = @($MsPlaywrightDir)
    $cacheAlt = Join-Path $env:USERPROFILE ".cache\ms-playwright"
    if ($cacheAlt -ne $MsPlaywrightDir) { $roots += $cacheAlt }

    $paths = @()
    foreach ($root in $roots) {
        $paths += (Join-Path $root "chromium-$ChromiumRevision\chrome-win\chrome.exe")
        $paths += (Join-Path $root "chromium-$ChromiumRevision\chrome-win64\chrome.exe")
    }
    return $paths
}

function Get-InstalledChromePath {
    foreach ($p in Get-ExpectedChromePaths) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

function Test-ChromiumInstalled {
    return $null -ne (Get-InstalledChromePath)
}

function Show-ChromiumStatus {
    Write-Host "Browser cache (expected): $MsPlaywrightDir\chromium-$ChromiumRevision\"
    Write-Host "Victoria profile (not the Chromium binary): $VictoriaProfile"
    $chrome = Get-InstalledChromePath
    if ($chrome) {
        Write-Host "FOUND: $chrome" -ForegroundColor Green
        return $true
    }

    Write-Host "MISSING: chromium-$ChromiumRevision chrome.exe (Host needs this exact revision)" -ForegroundColor Yellow
    # Helpful: show stray chromium folders so Kurt can see version mismatch.
    if (Test-Path -LiteralPath $MsPlaywrightDir) {
        $dirs = Get-ChildItem -Path $MsPlaywrightDir -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'chromium*' } |
            Select-Object -ExpandProperty Name
        if ($dirs) {
            Write-Host "Present under ms-playwright: $($dirs -join ', ')" -ForegroundColor Yellow
            Write-Host "(ALLSTART can say OK only when chromium-$ChromiumRevision chrome.exe exists.)"
        }
    }
    $cacheAlt = Join-Path $env:USERPROFILE ".cache\ms-playwright"
    if (Test-Path -LiteralPath $cacheAlt) {
        Write-Host "Note: also saw $cacheAlt - Host looks in %LOCALAPPDATA%\ms-playwright by default." -ForegroundColor Yellow
    }
    return $false
}

if ($VerifyOnly) {
    Write-Host "=== install-playwright: verify only (revision $ChromiumRevision) ==="
    if (Show-ChromiumStatus) { exit 0 }
    Write-Warning "Chromium $ChromiumRevision not installed. Re-run without -VerifyOnly."
    exit 1
}

# Fast path: correct revision already installed.
if (Test-ChromiumInstalled) {
    Write-Host "=== install-playwright: Chromium $ChromiumRevision already present ==="
    [void](Show-ChromiumStatus)
    Write-Host "Playwright Chromium ready." -ForegroundColor Green
    exit 0
}

Write-Host "=== install-playwright: build Host (Release) so playwright.ps1 matches runtime ==="
dotnet build $HostProj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Warning "dotnet build Host failed (exit $LASTEXITCODE) - trying Inference restore/build"
    dotnet restore $InferenceProj
    dotnet build $InferenceProj -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet build failed (exit $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# Prefer the Host Release CLI (same bits the running Host uses).
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
    Write-Host "SkipBrowserDownload set - package/build only."
    exit 0
}

Write-Host "=== install-playwright: download Chromium $ChromiumRevision (this can take several minutes) ==="
Write-Host "Target: $MsPlaywrightDir\chromium-$ChromiumRevision\"
Write-Host "Do NOT cancel - leave it running until FOUND."
# Channel=chromium on Host uses chrome.exe; still install default chromium package (includes shell).
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
    Write-Warning "Install reported success but chromium-$ChromiumRevision chrome.exe was not found"
    Write-Host "Check PLAYWRIGHT_BROWSERS_PATH if set: '$env:PLAYWRIGHT_BROWSERS_PATH'"
    exit 2
}

Write-Host "Playwright Chromium ready." -ForegroundColor Green
Write-Host "Next: .\ALLSTART.ps1 -RestartHost   then ask Victoria to open https://example.com"
exit 0
