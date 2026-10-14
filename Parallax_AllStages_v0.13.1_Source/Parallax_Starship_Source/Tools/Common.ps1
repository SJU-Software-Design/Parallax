# Shared path resolution for build and validation scripts. No files are changed here.
function Get-ParallaxProjectPath {
    $projectPath = Split-Path -Parent $PSScriptRoot
    if (-not (Test-Path -LiteralPath (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'))) {
        throw 'Run tooling from the complete Parallax Unity project (Assets, Packages and ProjectSettings).'
    }
    return $projectPath
}

function Get-ParallaxDeliveryPath {
    param([string]$ProjectPath, [switch]$Legacy, [ValidateSet('Windows','Mac')][string]$Platform='Windows')
    $platformFolder=if($Platform -eq 'Mac'){'맥'}else{'윈도우'}
    $buildFolder=if($Legacy){'이전버전'}else{'실행파일'}
    $workspaceDelivery = [IO.Path]::GetFullPath((Join-Path $ProjectPath ("..\..\"+$platformFolder+'\'+$buildFolder)))
    if (Test-Path -LiteralPath $workspaceDelivery -PathType Container) { return $workspaceDelivery }
    return (Join-Path $ProjectPath 'Builds')
}

function Resolve-ParallaxUnityEditor {
    param([string]$UnityEditor, [string]$ProjectPath)
    $versionLine = Get-Content -LiteralPath (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    $version = $versionLine -replace '^m_EditorVersion: ', ''
    if (-not $UnityEditor) { $UnityEditor = $env:UNITY_EDITOR_PATH }
    if (-not $UnityEditor) {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA "Unity\Editors\$version\Editor\Unity.exe"),
            (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe")
        )
        $UnityEditor = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (-not $UnityEditor -or -not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
        throw "Unity $version Editor was not found. Pass -UnityEditor with Unity.exe's full path, or set UNITY_EDITOR_PATH."
    }
    return (Resolve-Path -LiteralPath $UnityEditor).Path
}
