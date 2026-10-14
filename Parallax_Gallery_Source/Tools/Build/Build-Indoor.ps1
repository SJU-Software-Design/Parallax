param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$UnityEditor = Resolve-ParallaxUnityEditor -UnityEditor $UnityEditor -ProjectPath $projectDir
$indoorValidation = Join-Path $projectDir 'Validation\Indoor'
New-Item -ItemType Directory -Path $indoorValidation -Force | Out-Null
$indoorLog = Join-Path $indoorValidation 'build.log'
$indoorArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $projectDir + '"'), '-executeMethod', 'ShadeLink.Editor.BuildEntry.BuildIndoorWindows', '-logFile', ('"' + $indoorLog + '"'))
$indoorProcess = Start-Process -FilePath $UnityEditor -ArgumentList $indoorArgs -WindowStyle Hidden -PassThru
$indoorProcess.WaitForExit()
if ($indoorProcess.ExitCode -ne 0) { Get-Content -LiteralPath $indoorLog -Tail 70; throw "Indoor build failed: $($indoorProcess.ExitCode)" }
Get-Content -LiteralPath (Join-Path $indoorValidation 'build-result.txt')
