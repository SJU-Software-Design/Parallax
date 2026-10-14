param([string]$GameExecutable, [switch]$Blockout)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$deliveryDir = Get-ParallaxDeliveryPath -ProjectPath $projectDir
if (-not $GameExecutable) {
    $relative = if ($Blockout) { 'Parallax_Unity_v0.12.0_Library_Blockout\Parallax_Blockout.exe' } else { 'Parallax_Unity_v0.12.0_Library\Parallax.exe' }
    $GameExecutable = Join-Path $deliveryDir $relative
}
if (-not (Test-Path -LiteralPath $GameExecutable -PathType Leaf)) { throw 'Build the Library Windows player first, or pass -GameExecutable.' }
$outputDir = Join-Path $projectDir $(if ($Blockout) { 'Validation\Library\Blockout' } else { 'Validation\Library\Player' })
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$arguments = '-library-test -shade-output "' + $outputDir + '" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "' + (Join-Path $outputDir 'player.log') + '"'
$startedAt = Get-Date
$process = Start-Process -FilePath $GameExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(600000)) { Stop-Process -Id $process.Id; throw 'Library validation timed out.' }
$reportPath = Join-Path $outputDir 'library-results.json'
if (-not (Test-Path -LiteralPath $reportPath)) { throw 'The Library player did not produce a validation report.' }
if ((Get-Item -LiteralPath $reportPath).LastWriteTime -lt $startedAt) { throw 'The Library player did not update the validation report.' }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Get-Content -LiteralPath $reportPath
if ($process.ExitCode -ne 0 -or -not $report.passed) { throw 'Library validation failed.' }
