param([string]$GameExecutable)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$deliveryDir = Get-ParallaxDeliveryPath -ProjectPath $projectDir
if (-not $GameExecutable) { $GameExecutable = Join-Path $deliveryDir 'Parallax_Unity_v0.8.2_Gallery\Parallax.exe' }
if (-not (Test-Path -LiteralPath $GameExecutable -PathType Leaf)) { throw 'Build the Gallery Windows player first, or pass -GameExecutable.' }
$outputDir = Join-Path $projectDir 'Validation\GalleryPerspective'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$arguments = '-gallery-perspective-test -shade-output "' + $outputDir + '" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "' + (Join-Path $outputDir 'player.log') + '"'
$process = Start-Process -FilePath $GameExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(240000)) { Stop-Process -Id $process.Id; throw 'Gallery perspective validation timed out' }
$reportPath = Join-Path $outputDir 'perspective-results.json'
if (-not (Test-Path -LiteralPath $reportPath)) { throw 'The Gallery player did not produce a validation report.' }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Get-Content -LiteralPath $reportPath
if ($process.ExitCode -ne 0 -or -not $report.passed) { throw 'Gallery perspective validation failed' }
