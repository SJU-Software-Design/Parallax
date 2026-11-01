param([string]$UnityEditor, [switch]$Blockout, [switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$editor = Resolve-ParallaxUnityEditor -UnityEditor $UnityEditor -ProjectPath $projectDir
$method = if ($PrepareOnly) { 'Prepare' } else { 'Build' }
if ($Blockout) { $method += 'Blockout' }
$logName = if ($Blockout) { 'blockout-build.log' } else { 'build.log' }
$log = Join-Path $projectDir ('Validation\Library\' + $logName)
New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
$arguments = '-batchmode -nographics -quit -projectPath "' + $projectDir + '" -executeMethod ShadeLink.Editor.LibraryBuild.' + $method + ' -logFile "' + $log + '"'
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Library $method failed ($($process.ExitCode)). See $log" }
$marker = if ($PrepareOnly) { 'LIBRARY_PREPARE_OK' } else { 'LIBRARY_BUILD_OK' }
if (-not (Select-String -LiteralPath $log -Pattern $marker -SimpleMatch -Quiet)) {
    throw "Library $method did not complete. See $log"
}
Write-Output "Library $method completed. $log"
