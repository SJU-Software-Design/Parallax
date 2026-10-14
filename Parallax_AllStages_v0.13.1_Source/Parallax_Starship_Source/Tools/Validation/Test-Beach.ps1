param([string]$GameExecutable)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir=Get-ParallaxProjectPath
$deliveryDir=Get-ParallaxDeliveryPath -ProjectPath $projectDir
if(-not $GameExecutable){$GameExecutable=Join-Path $deliveryDir 'Parallax_Unity_v0.12.1_Beach\Parallax.exe'}
$outputDir=Join-Path $projectDir 'Validation\Beach\Player'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$arguments='-beach-test -shade-output "'+$outputDir+'" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "'+(Join-Path $outputDir 'player.log')+'"'
$process=Start-Process -FilePath $GameExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(240000)){Stop-Process -Id $process.Id;throw 'Beach validation timed out'}
$reportPath=Join-Path $outputDir 'beach-results.json'
if(-not(Test-Path -LiteralPath $reportPath)){throw 'No Beach validation report was produced'}
$report=Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8
if($process.ExitCode -ne 0 -or -not $report.passed){throw 'Beach validation failed'}
