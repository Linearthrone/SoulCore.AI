# QA-01 F2 git tracking check
$ErrorActionPreference = 'Stop'
Set-Location 'C:\Users\kurtw\Soul_Core'

Write-Output '===== git status of SoulCore/.env ====='
$tracked = git ls-files --error-unmatch SoulCore/.env 2>&1
$exitCode = $LASTEXITCODE
Write-Output ("git ls-files exit code: " + $exitCode)
Write-Output ("git ls-files output: " + $tracked)

Write-Output ''
Write-Output '===== git check-ignore SoulCore/.env ====='
$ignored = git check-ignore -v SoulCore/.env 2>&1
Write-Output ("git check-ignore exit code: " + $LASTEXITCODE)
Write-Output ("git check-ignore output: " + $ignored)

Write-Output ''
Write-Output '===== git tracked files matching .env (should be only .env.example/.env.template) ====='
$envFiles = git ls-files 'SoulCore/.env*' 2>&1
Write-Output ("tracked .env* files: " + $envFiles)

Write-Output ''
Write-Output '===== is SoulCore/.env in working tree but untracked? ====='
$statusLine = (git status --porcelain SoulCore/.env 2>&1)
Write-Output ("status porcelain: '" + $statusLine + "' (empty = not showing, likely ignored)")
