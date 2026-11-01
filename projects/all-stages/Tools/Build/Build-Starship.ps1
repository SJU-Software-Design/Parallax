param([string]$UnityEditor)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir=Get-ParallaxProjectPath
$editor=Resolve-ParallaxUnityEditor -UnityEditor $UnityEditor -ProjectPath $projectDir
$log=Join-Path $projectDir 'Validation\Starship\build.log'
New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
$arguments='-batchmode -nographics -quit -projectPath "'+$projectDir+'" -executeMethod ShadeLink.Editor.StarshipBuild.Build -logFile "'+$log+'"'
$process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if($process.ExitCode -ne 0){throw "Starship build failed. See $log"}
Write-Output "STARSHIP_BUILD_COMPLETED $log"
