param([ValidateSet('Beach','Gallery','Library')][string]$Stage='Beach')
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir=Get-ParallaxProjectPath
$deliveryDir=Get-ParallaxDeliveryPath -ProjectPath $projectDir
$folder=if($Stage -eq 'Gallery'){'MirrorGallery'}else{$Stage}
$version=if($Stage -eq 'Beach'){'0.12.1'}else{'0.12.0'}
$gameExecutable=Join-Path $deliveryDir ('Parallax_Unity_v'+$version+'_'+$folder+'\Parallax.exe')
$outputDir=Join-Path $projectDir ('Validation\Placement\'+$Stage)
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$arguments='-placement-test -shade-output "'+$outputDir+'" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "'+(Join-Path $outputDir 'player.log')+'"'
$started=Get-Date
$process=Start-Process -FilePath $gameExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(180000)){Stop-Process -Id $process.Id;throw 'Placement validation timed out'}
$path=Join-Path $outputDir 'placement-results.json'
if(-not(Test-Path -LiteralPath $path)){throw 'No placement validation report'}
if((Get-Item -LiteralPath $path).LastWriteTime -lt $started){throw 'Stale placement validation report'}
$report=Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
$report | ConvertTo-Json -Depth 4
if($process.ExitCode -ne 0 -or -not $report.passed){throw 'Placement validation failed'}
