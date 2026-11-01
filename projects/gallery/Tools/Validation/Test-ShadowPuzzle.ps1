param([string]$GameExecutable)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$deliveryDir = Get-ParallaxDeliveryPath -ProjectPath $projectDir
if (-not $GameExecutable) {
    $GameExecutable = (Join-Path $deliveryDir 'Parallax_Unity_v0.6_Key\Parallax.exe')
}
if (-not (Test-Path -LiteralPath $GameExecutable)) { throw 'Build the Windows player first.' }
$shadowOutput = Join-Path $projectDir 'Validation\GalleryRegressionV06'
New-Item -ItemType Directory -Path $shadowOutput -Force | Out-Null
$gameArguments = @('-shadow-smoke', '-shade-output', ('"' + $shadowOutput + '"'),
    '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900',
    '-logFile', ('"' + (Join-Path $shadowOutput 'player.log') + '"'))
$gameProcess = Start-Process -FilePath $GameExecutable -ArgumentList $gameArguments -WindowStyle Hidden -PassThru
if (-not $gameProcess.WaitForExit(180000)) {
    Stop-Process -Id $gameProcess.Id
    throw 'Shadow puzzle validation exceeded 180 seconds.'
}
$reportPath = Join-Path $shadowOutput 'shadow-smoke-results.json'
if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Unity did not produce a shadow test report.' }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($gameProcess.ExitCode -ne 0 -or -not $report.passed) {
    Get-Content -LiteralPath $reportPath -Raw
    throw 'Shadow puzzle player validation failed.'
}
Write-Output "Shadow puzzle checks passed: $($report.checks.Count)"
Write-Output "Unity: $($report.unity) / GPU: $($report.gpu)"
