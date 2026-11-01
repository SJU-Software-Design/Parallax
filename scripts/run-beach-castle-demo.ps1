[CmdletBinding()]
param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
$taskArgs = @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',
    (Join-Path $PSScriptRoot 'run-stage.ps1'),'-Stage','Beach','-BuildOnly')
if ($UnityEditor) { $taskArgs += @('-UnityEditor',$UnityEditor) }
& (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') @taskArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskRepo = Split-Path -Parent $PSScriptRoot
$taskOutput = Join-Path $taskRepo 'Builds\Latest\Beach'
# This is an explicitly requested interactive game, so its window is visible.
Start-Process -FilePath (Join-Path $taskOutput 'Parallax.exe') -WorkingDirectory $taskOutput -ArgumentList '-beach-castle-demo' | Out-Null
