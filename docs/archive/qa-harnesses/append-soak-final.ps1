#Requires -Version 5.1
<#
.SYNOPSIS
  Wait for a soak PID to exit, then append a markdownlint-clean Final section to an OPS report.
.PARAMETER MetaPath
  JSON meta written at soak launch (soak_pid, soak_log, report_path).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MetaPath
)

$ErrorActionPreference = "Continue"

if (-not (Test-Path -LiteralPath $MetaPath)) {
    throw "Meta missing: $MetaPath"
}

$meta = Get-Content -LiteralPath $MetaPath -Raw -Encoding utf8 | ConvertFrom-Json
$soakPid = [int]$meta.soak_pid
$reportPath = [string]$meta.report_path
$soakLog = [string]$meta.soak_log

if ([string]::IsNullOrWhiteSpace($reportPath)) {
    throw "meta.report_path is required"
}

Wait-Process -Id $soakPid -ErrorAction SilentlyContinue

$tail = @()
$summaryLine = $null
$abortLine = $null
if ($soakLog -and (Test-Path -LiteralPath $soakLog)) {
    $all = Get-Content -LiteralPath $soakLog -ErrorAction SilentlyContinue
    if ($all) {
        $tail = $all | Select-Object -Last 40
        $summaryLine = ($all | Where-Object { $_ -match '\] SUMMARY ' } | Select-Object -Last 1)
        $abortLine = ($all | Where-Object { $_ -match '\] ABORT: ' } | Select-Object -Last 1)
    }
}

$healthRaw = ""
try {
    $healthRaw = (Invoke-WebRequest -Uri "http://127.0.0.1:7700/health" -UseBasicParsing -TimeoutSec 5).Content
} catch {
    $msg = ($_.Exception.Message -replace '"', '')
    $healthRaw = "{`"error`":`"HEALTH_UNREACHABLE`",`"message`":`"$msg`"}"
}

$healthPretty = $healthRaw
try {
    $healthPretty = ($healthRaw | ConvertFrom-Json | ConvertTo-Json -Depth 8)
} catch {
    # keep raw
}

$verdict = "Unclear - OPS-01 confirm vs soak-runbook.md abort criteria"
$pass = $null
if ($summaryLine -match 'SUMMARY\s+(\{.+\})\s*$') {
    try {
        $sum = $matches[1] | ConvertFrom-Json
        if ($sum.pass -eq $true) {
            $verdict = "Pass"
            $pass = $true
        } elseif ($sum.pass -eq $false) {
            $reason = $sum.abortReason
            if (-not $reason -and $abortLine -match 'ABORT:\s*(.+)$') {
                $reason = $matches[1].Trim()
            }
            $verdict = "Fail - $reason"
            $pass = $false
        }
    } catch {
        # keep unclear
    }
}

$ended = Get-Date
$fenceJson = '```json'
$fenceText = '```text'
$fenceEnd = '```'

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Final (auto-appended by soak watcher)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Field | Value |")
[void]$sb.AppendLine("| --- | --- |")
[void]$sb.AppendLine("| Ended (local) | $($ended.ToString('yyyy-MM-dd HH:mm:ss')) |")
[void]$sb.AppendLine("| Ended (UTC) | $($ended.ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss'))Z |")
[void]$sb.AppendLine("| Soak PID | $soakPid (exited) |")
[void]$sb.AppendLine("| Soak log | ``$soakLog`` |")
[void]$sb.AppendLine("| Verdict | **$verdict** |")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Final /health")
[void]$sb.AppendLine("")
[void]$sb.AppendLine($fenceJson)
[void]$sb.AppendLine($healthPretty)
[void]$sb.AppendLine($fenceEnd)
[void]$sb.AppendLine("")
[void]$sb.AppendLine("### Soak log tail (last 40 lines)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine($fenceText)
foreach ($line in $tail) {
    [void]$sb.AppendLine($line)
}
[void]$sb.AppendLine($fenceEnd)
[void]$sb.AppendLine("")

Add-Content -LiteralPath $reportPath -Value $sb.ToString() -Encoding utf8

if ($soakLog -and (Test-Path -LiteralPath $soakLog)) {
    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -LiteralPath $soakLog -Value "[$stamp] WATCHER: final section appended to report (pass=$pass)" -Encoding utf8
}
