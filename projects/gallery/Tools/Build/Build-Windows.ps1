param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$UnityEditor = Resolve-ParallaxUnityEditor -UnityEditor $UnityEditor -ProjectPath $projectDir
$validationDir = Join-Path $projectDir 'Validation'
New-Item -ItemType Directory -Path $validationDir -Force | Out-Null
$logPath = Join-Path $validationDir 'build.log'
$unityArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $projectDir + '"'),
    '-executeMethod', 'ShadeLink.Editor.BuildEntry.BuildWindows', '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $logPath -Tail 80; throw "Unity build failed: $($process.ExitCode)" }
Get-Content -LiteralPath (Join-Path $validationDir 'build-result.txt')
