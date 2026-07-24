<#
.SYNOPSIS
  Create an empty SoulCore Memory DB from Schema/001_schema.sql (evidence / CI).

.NOTES
  - Does not write to LLMOD Data/ databases.
  - Overwrites local data/soulcore_memory.empty.db only.
#>
[CmdletBinding()]
param(
    [string]$Sqlite3Path = "",
    [string]$OutDb = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Schema = Join-Path $Root "Schema\001_schema.sql"
$Migration = Join-Path $Root "Migrations\001_initial.sql"
$DataDir = Join-Path $Root "data"

if (-not $OutDb) {
    $OutDb = Join-Path $DataDir "soulcore_memory.empty.db"
}

if (-not (Test-Path $Schema)) {
    throw "Schema not found: $Schema"
}
if (-not (Test-Path $Migration)) {
    throw "Migration not found: $Migration"
}

function Resolve-Sqlite3 {
    param([string]$Preferred)
    if ($Preferred -and (Test-Path $Preferred)) { return (Resolve-Path $Preferred).Path }
    $cmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "sqlite3.exe not found. Pass -Sqlite3Path or add sqlite3 to PATH."
}

$sqlite3 = Resolve-Sqlite3 -Preferred $Sqlite3Path
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null

if (Test-Path $OutDb) {
    Remove-Item -Force $OutDb
}

Write-Host "sqlite3:   $sqlite3"
Write-Host "schema:    $Schema"
Write-Host "migration: $Migration"
Write-Host "out:       $OutDb"

# Apply canonical schema, then ledger via Migrations/001_initial.sql (single source of truth)
& $sqlite3 $OutDb ".read `"$Schema`""
if ($LASTEXITCODE -ne 0) { throw "schema apply failed (exit $LASTEXITCODE)" }

& $sqlite3 $OutDb ".read `"$Migration`""
if ($LASTEXITCODE -ne 0) { throw "migration ledger apply failed (exit $LASTEXITCODE)" }

Write-Host ""
Write-Host "=== tables ==="
& $sqlite3 $OutDb ".tables"

Write-Host ""
Write-Host "=== schema (names) ==="
& $sqlite3 $OutDb "SELECT name, type FROM sqlite_master WHERE type IN ('table','index') AND name NOT LIKE 'sqlite_%' ORDER BY type, name;"

Write-Host ""
Write-Host "=== emotion_state seed ==="
& $sqlite3 $OutDb "SELECT id, valence, arousal, dominance, updated_at, revision FROM emotion_state;"

Write-Host ""
Write-Host "=== schema_migrations ==="
& $sqlite3 $OutDb "SELECT version, name, applied_at FROM schema_migrations;"

Write-Host ""
Write-Host "=== db file ==="
Get-Item $OutDb | Format-List FullName, Length, LastWriteTime

Write-Host "OK: empty DB created."
