# _qa_probe_backends.ps1
# Probe Ollama and Hermes inference backends to determine reachability.
$ErrorActionPreference = 'Continue'

Write-Output '===== Ollama :11434 ====='
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 5 -UseBasicParsing
    Write-Output ("Ollama reachable: status=" + $r.StatusCode)
    Write-Output ("Body: " + $r.Content.Substring(0, [Math]::Min(300, $r.Content.Length)))
} catch {
    Write-Output ("Ollama probe failed: " + $_.Exception.Message)
}

Write-Output ''
Write-Output '===== Hermes :8642 ====='
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8642/health' -TimeoutSec 5 -UseBasicParsing
    Write-Output ("Hermes reachable: status=" + $r.StatusCode)
    Write-Output ("Body: " + $r.Content.Substring(0, [Math]::Min(300, $r.Content.Length)))
} catch {
    Write-Output ("Hermes probe failed: " + $_.Exception.Message)
}

Write-Output ''
Write-Output '===== Port listen check ====='
$ports = @(11434, 8642, 8888, 7700)
foreach ($p in $ports) {
    $tcp = Test-NetConnection -ComputerName 127.0.0.1 -Port $p -WarningAction SilentlyContinue
    Write-Output ("Port " + $p + ": TcpTestSucceeded=" + $tcp.TcpTestSucceeded)
}
