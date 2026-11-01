param([string]$GameExecutable,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir=Get-ParallaxProjectPath
if(-not $GameExecutable){$GameExecutable=Join-Path (Get-ParallaxDeliveryPath -ProjectPath $projectDir) 'Parallax_Unity_v0.12.1_Beach\Parallax.exe'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $projectDir 'Validation\BeachWater\Fixed'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$arguments='-beach-water-test -shade-output "'+$OutputDirectory+'" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "'+(Join-Path $OutputDirectory 'player.log')+'"'
$process=Start-Process -FilePath $GameExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(120000)){Stop-Process -Id $process.Id;throw 'Beach water recovery validation timed out'}
$reportPath=Join-Path $OutputDirectory 'water-results.json'
if(-not(Test-Path -LiteralPath $reportPath)){throw 'No water recovery report was produced'}
$report=Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8
if($process.ExitCode -ne 0 -or -not $report.passed){throw 'Beach water recovery validation failed'}
