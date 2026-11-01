param([string]$GameExecutable)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$deliveryDir = Get-ParallaxDeliveryPath -ProjectPath $projectDir -Legacy
if (-not $GameExecutable) {
    $GameExecutable = (Join-Path $deliveryDir 'Parallax_Unity_v0.6_Key\Parallax.exe')
}
if (-not (Test-Path -LiteralPath $GameExecutable)) { throw 'Build the Windows player first.' }
$keyOutput = Join-Path $projectDir 'Validation\KeyPuzzle'
New-Item -ItemType Directory -Path $keyOutput -Force | Out-Null
$keyArgs = @('-key-smoke', '-shade-offscreen', '-shade-output', ('"' + $keyOutput + '"'),
    '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
    '-logFile', ('"' + (Join-Path $keyOutput 'player.log') + '"'))
$keyProcess = Start-Process -FilePath $GameExecutable -ArgumentList $keyArgs -WindowStyle Hidden -PassThru
if (-not $keyProcess.WaitForExit(180000)) { Stop-Process -Id $keyProcess.Id; throw 'Key validation exceeded 180 seconds.' }
$keyReportPath = Join-Path $keyOutput 'key-smoke-results.json'
if (-not (Test-Path -LiteralPath $keyReportPath)) { throw 'Unity did not produce a key report.' }
$keyReport = Get-Content -LiteralPath $keyReportPath -Raw | ConvertFrom-Json
if ($keyProcess.ExitCode -ne 0 -or -not $keyReport.passed) { Get-Content -LiteralPath $keyReportPath -Raw; throw 'Key player validation failed.' }
Write-Output "Key checks passed: $($keyReport.checks.Count)"
Write-Output "Unity: $($keyReport.unity) / GPU: $($keyReport.gpu)"
