# _qa_run_gate.ps1
# Wrapper to run an E2E gate script with an overall process timeout.
# Captures full output even if the script hangs.
# Usage: _qa_run_gate.ps1 -Script <path> [-ExtraArgs "-Force"]
param(
    [Parameter(Mandatory)][string]$Script,
    [string]$ExtraArgs = '-Force',
    [int]$TimeoutSec = 60
)

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = 'powershell.exe'
$psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$Script`" $ExtraArgs"
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true

$proc = New-Object System.Diagnostics.Process
$proc.StartInfo = $psi
$proc.EnableRaisingEvents = $true
$sb = New-Object System.Text.StringBuilder
$errSb = New-Object System.Text.StringBuilder
$null = Register-ObjectEvent -InputObject $proc -EventName 'OutputDataReceived' -Action { if ($EventArgs.Data) { $null = $Event.MessageData.AppendLine($EventArgs.Data) } } -MessageData $sb
$null = Register-ObjectEvent -InputObject $proc -EventName 'ErrorDataReceived' -Action { if ($EventArgs.Data) { $null = $Event.MessageData.AppendLine($EventArgs.Data) } } -MessageData $errSb

[void]$proc.Start()
$proc.BeginOutputReadLine()
$proc.BeginErrorReadLine()

$exited = $proc.WaitForExit($TimeoutSec * 1000)
if (-not $exited) {
    Write-Output "WRAPPER_TIMEOUT: process did not exit within $TimeoutSec sec; killing."
    try { $proc.Kill() } catch { }
    Start-Sleep -Milliseconds 500
}

$exitCode = if ($proc.HasExited) { $proc.ExitCode } else { -1 }
Write-Output $sb.ToString()
if ($errSb.Length -gt 0) {
    Write-Output '--- STDERR ---'
    Write-Output $errSb.ToString()
}
Write-Output "WRAPPER_EXIT_CODE=$exitCode"
Write-Output "WRAPPER_EXITED=$exited"
