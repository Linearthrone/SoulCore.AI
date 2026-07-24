# QA-01 F4 soak snapshot — read-only tail of soak log + disk + health
$ErrorActionPreference = 'Continue'
$logPath = 'C:\Users\kurtw\Soul_Core\SoulCore\scripts\logs\soak-20260723-013126.log'

Write-Output '===== F4: SOAK SNAPSHOT ====='
Write-Output ('SnapshotTime_UTC: ' + [DateTime]::UtcNow.ToString('o'))
Write-Output ('SnapshotTime_Local: ' + (Get-Date).ToString('o'))

# 1. Log existence + size + last write
if (Test-Path $logPath) {
    $fi = Get-Item $logPath
    Write-Output ('LogPath: ' + $logPath)
    Write-Output ('LogSizeKB: ' + [math]::Round($fi.Length/1KB, 2))
    Write-Output ('LogLastWriteUtc: ' + $fi.LastWriteTimeUtc.ToString('o'))
    Write-Output ('LogCreationUtc: ' + $fi.CreationTimeUtc.ToString('o'))
} else {
    Write-Output ('LogPath: ' + $logPath + '  *** NOT FOUND ***')
}

# 2. Probe count — count occurrences of probe markers. Soak scripts typically log
#    a health probe per tick. Count a few candidate markers and pick the max.
$lines = @()
if (Test-Path $logPath) {
    $lines = Get-Content -Path $logPath -ErrorAction SilentlyContinue
}
Write-Output ('LogTotalLines: ' + $lines.Count)

$probeMarkers = @('probe', '/health', 'health ok', 'ok ', 'tick', 'heartbeat', 'PING', 'GET /health')
$maxCount = 0
$maxMarker = ''
foreach ($m in $probeMarkers) {
    $c = ($lines | Select-String -Pattern ([regex]::Escape($m)) -SimpleMatch:$false -ErrorAction SilentlyContinue).Count
    if ($c -gt $maxCount) { $maxCount = $c; $maxMarker = $m }
}
Write-Output ('ProbeMarker_best: ' + $maxMarker + ' = ' + $maxCount)

# Count explicit /health hits if present
$healthHits = ($lines | Select-String -Pattern '/health' -ErrorAction SilentlyContinue).Count
Write-Output ('HealthEndpointHits: ' + $healthHits)

# 3. Error streaks — count error/warn/fail/exception lines
$errLines = $lines | Select-String -Pattern '(?i)(error|fail|exception|warn|critical|fatal|timeout|refused)' -ErrorAction SilentlyContinue
Write-Output ('ErrorWarnLines: ' + $errLines.Count)
if ($errLines.Count -gt 0) {
    Write-Output '--- last 10 error/warn lines ---'
    $errLines | Select-Object -Last 10 | ForEach-Object { Write-Output ('  L' + $_.LineNumber + ': ' + $_.Line) }
}

# 4. Last 20 lines of the log (tail)
Write-Output ''
Write-Output '--- soak log tail (last 20 lines) ---'
$tail = $lines | Select-Object -Last 20
$i = $lines.Count - 20
foreach ($l in $tail) { $i++; if ($i -lt 1) { $i = 1 }; Write-Output ('  L' + $i + ': ' + $l) }

# 5. First 5 lines (header / start banner)
Write-Output ''
Write-Output '--- soak log head (first 5 lines) ---'
$head = $lines | Select-Object -First 5
$hi = 0
foreach ($l in $head) { $hi++; Write-Output ('  L' + $hi + ': ' + $l) }

# 6. Disk free
Write-Output ''
Write-Output '--- disk free ---'
$disk = Get-PSDrive -Name C -ErrorAction SilentlyContinue
if ($disk) {
    Write-Output ('DiskFreeGB_C: ' + [math]::Round($disk.Free/1GB, 2))
    Write-Output ('DiskUsedGB_C: ' + [math]::Round($disk.Used/1GB, 2))
}

# 7. Current /health
Write-Output ''
Write-Output '--- current /health ---'
try {
    $h = Invoke-RestMethod -Uri 'http://127.0.0.1:7700/health' -TimeoutSec 8
    Write-Output ('status=' + $h.status)
    Write-Output ('memory.open=' + $h.memory.open)
    Write-Output ('inference.provider=' + $h.inference.provider)
    Write-Output ('hermes.enabled=' + $h.hermes.enabled)
    Write-Output ('soulLoop.enabled=' + $h.soulLoop.enabled)
    Write-Output ('unreal.target=' + $h.unreal.target)
    Write-Output ('unreal.connected=' + $h.unreal.connected)
} catch {
    Write-Output ('/health ERROR: ' + $_.Exception.Message)
}

# 8. Process check (PID 47288) — read-only, do NOT touch
Write-Output ''
Write-Output '--- soak process check (PID 47288, read-only) ---'
try {
    $proc = Get-Process -Id 47288 -ErrorAction Stop
    Write-Output ('PID 47288: alive, Name=' + $proc.ProcessName + ', StartTime=' + $proc.StartTime.ToString('o'))
} catch {
    Write-Output ('PID 47288: ' + $_.Exception.Message)
}
