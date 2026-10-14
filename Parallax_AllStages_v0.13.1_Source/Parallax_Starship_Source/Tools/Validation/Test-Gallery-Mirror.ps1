param([string]$GameExecutable)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir=Get-ParallaxProjectPath
$deliveryDir=Get-ParallaxDeliveryPath -ProjectPath $projectDir
if(-not $GameExecutable){$GameExecutable=Join-Path $deliveryDir 'Parallax_Unity_v0.12.0_MirrorGallery\Parallax.exe'}
if(-not(Test-Path -LiteralPath $GameExecutable -PathType Leaf)){throw 'Build the mirror Gallery player first.'}
$outputDir=Join-Path $projectDir 'Validation\GalleryMirror'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$reportPath=Join-Path $outputDir 'mirror-results.json'
if(Test-Path -LiteralPath $reportPath){Move-Item -LiteralPath $reportPath -Destination (Join-Path $outputDir ('mirror-results-before-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.json'))}
$arguments='-gallery-mirror-test -shade-output "'+$outputDir+'" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "'+(Join-Path $outputDir 'player.log')+'"'
$process=Start-Process -FilePath $GameExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(240000)){Stop-Process -Id $process.Id;throw 'Gallery mirror validation timed out'}
if(-not(Test-Path -LiteralPath $reportPath)){throw 'The mirror Gallery player did not produce a report.'}
$report=Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
Get-Content -LiteralPath $reportPath -Encoding UTF8
if($process.ExitCode -ne 0 -or -not $report.passed){throw 'Gallery mirror validation failed'}
