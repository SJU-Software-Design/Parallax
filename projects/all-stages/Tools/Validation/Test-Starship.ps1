param([string]$GameExecutable)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir=Get-ParallaxProjectPath
if(-not $GameExecutable){
    $versionSource=Get-Content -LiteralPath (Join-Path $projectDir 'Assets\Parallax\Starship\Scripts\StarshipGame.cs') -Raw
    $version=[regex]::Match($versionSource,'public const string Version="([^"]+)"').Groups[1].Value
    if(-not $version){throw 'Cannot determine the Starship version'}
    $GameExecutable=Join-Path (Get-ParallaxDeliveryPath -ProjectPath $projectDir) ('Parallax_Unity_v'+$version+'_Starship\Parallax.exe')
}
$outputDir=Join-Path $projectDir 'Validation\Starship\Player'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$arguments='-starship-test -shade-output "'+$outputDir+'" -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -logFile "'+(Join-Path $outputDir 'player.log')+'"'
$process=Start-Process -FilePath $GameExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(300000)){Stop-Process -Id $process.Id;throw 'Starship validation timed out'}
$reportPath=Join-Path $outputDir 'starship-results.json'
if(-not(Test-Path -LiteralPath $reportPath)){throw 'No starship runtime validation report'}
$report=Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
[pscustomobject]@{Passed=$report.passed;Checks=$report.checks.Count;Errors=$report.errors.Count;Failure=$report.failure} | ConvertTo-Json
if($process.ExitCode -ne 0 -or -not $report.passed){throw 'Starship runtime validation failed'}
