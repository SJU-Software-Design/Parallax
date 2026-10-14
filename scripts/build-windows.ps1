param([string]$UnityEditor)
$ErrorActionPreference = 'Stop'

$repoDir = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoDir 'Parallax_Gallery_Source'
. (Join-Path $projectDir 'Tools/Common.ps1')
$editor = Resolve-ParallaxUnityEditor -UnityEditor $UnityEditor -ProjectPath $projectDir
$outputDir = Join-Path $repoDir 'Builds/Windows'
$logFile = Join-Path $outputDir 'build.log'
$resultFile = Join-Path $outputDir 'build-result.txt'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
if (Test-Path -LiteralPath $resultFile) { Remove-Item -LiteralPath $resultFile }

$unityArgs = @('-batchmode', '-nographics', '-quit', '-buildTarget', 'StandaloneWindows64',
    '-standaloneBuildSubtarget', 'Player',
    '-projectPath', ('"' + $projectDir + '"'),
    '-executeMethod', 'ShadeLink.Editor.GalleryPlatformBuild.BuildWindows',
    '-logFile', ('"' + $logFile + '"'))
Write-Output "Building Gallery for Windows. Log: $logFile"
$process = Start-Process -FilePath $editor -ArgumentList $unityArgs -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Windows build failed ($($process.ExitCode)). See $logFile" }
if (-not (Test-Path -LiteralPath (Join-Path $outputDir 'Parallax.exe') -PathType Leaf) -or
    -not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
    throw "Unity did not produce a successful Windows build. See $logFile"
}
if ((Get-Content -LiteralPath $resultFile -Raw) -notmatch '(?m)^Result: Succeeded\r?$') {
    throw "Windows build did not succeed. See $logFile"
}
Write-Output "Built: $(Join-Path $outputDir 'Parallax.exe')"
