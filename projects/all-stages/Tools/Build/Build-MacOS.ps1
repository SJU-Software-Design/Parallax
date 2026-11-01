param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$UnityEditor = Resolve-ParallaxUnityEditor -UnityEditor $UnityEditor -ProjectPath $projectDir
$macSupport = Join-Path (Split-Path -Parent $UnityEditor) 'Data\PlaybackEngines\MacStandaloneSupport'
if (-not (Test-Path -LiteralPath $macSupport)) {
    throw 'Install Mac Build Support (Mono) for Unity 6000.3.24f1 before running this script.'
}
$validationDir = Join-Path $projectDir 'Validation'
New-Item -ItemType Directory -Path $validationDir -Force | Out-Null
$logPath = Join-Path $validationDir 'build-macos.log'
$unityArgs = @('-batchmode', '-nographics', '-quit', '-buildTarget', 'StandaloneOSX',
    '-projectPath', ('"' + $projectDir + '"'), '-executeMethod', 'ShadeLink.Editor.BuildEntry.BuildMacOS',
    '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $logPath -Tail 80; throw "Unity Mac build failed: $($process.ExitCode)" }
Get-Content -LiteralPath (Join-Path $validationDir 'build-macos-result.txt')
