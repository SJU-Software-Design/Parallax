param([string]$UnityEditor,[ValidateSet('All','Beach','Gallery','Library')][string]$Stage='All')
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir=Get-ParallaxProjectPath
$editor=Resolve-ParallaxUnityEditor -UnityEditor $UnityEditor -ProjectPath $projectDir
$log=Join-Path $projectDir ('Validation\Placement\build-'+$Stage+'.log')
New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
$method=if($Stage -eq 'All'){'ShadeLink.Editor.PlacementBuild.BuildAll'}else{'ShadeLink.Editor.'+$Stage+'Build.Build'}
$arguments='-batchmode -nographics -quit -projectPath "'+$projectDir+'" -executeMethod '+$method+' -logFile "'+$log+'"'
$process=Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if($process.ExitCode -ne 0){throw "Placement build failed. See $log"}
Write-Output "Placement build completed. $log"
