[CmdletBinding()]
param(
    [switch]$BuildOnly,
    [switch]$Run,
    [switch]$Rebuild,
    [string]$UnityEditor,
    [string]$WorkspaceRoot
)
$ErrorActionPreference = 'Stop'
if ($BuildOnly -and $Run) { throw 'Choose -BuildOnly or -Run, not both.' }
$repoDir = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sourceDir = Join-Path $repoDir 'projects\all-stages'
$inputFolders = @('Assets','Packages','ProjectSettings','Documentation')
$requiredUnity = '6000.3.24f1'

function Full-Path([string]$Path) {
    if (![IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $repoDir $Path }
    $full = [IO.Path]::GetFullPath($Path)
    if ($full -eq [IO.Path]::GetPathRoot($full)) { return $full }
    return $full.TrimEnd('\','/')
}
function Assert-NoLinks([string]$Path) {
    $probe = [IO.Path]::GetFullPath($Path)
    while ($probe) {
        if (Test-Path -LiteralPath $probe) {
            $item = Get-Item -LiteralPath $probe -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Generated paths must not traverse a junction or symlink: $probe"
            }
        }
        $parent = Split-Path -Parent $probe
        if (!$parent -or $parent -eq $probe) { break }
        $probe = $parent
    }
}
function Is-Within([string]$Path, [string]$Root) {
    $child = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    $parent = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    return $child.StartsWith($parent + '\', [StringComparison]::OrdinalIgnoreCase)
}
function Assert-Generated([string]$Path) {
    $full = Full-Path $Path
    if (!(Is-Within $full $workspace) -or $full -eq $sourceDir -or
        (Is-Within $full $sourceDir) -or (Is-Within $sourceDir $full)) {
        throw "Refusing a generated target outside the workspace or overlapping source: $full"
    }
    Assert-NoLinks $full
    return $full
}
function Remove-Generated([string]$Path) {
    $checked = Assert-Generated $Path
    if (Test-Path -LiteralPath $checked) {
        foreach ($node in (Get-ChildItem -LiteralPath $checked -Force -Recurse)) {
            if (($node.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing recursive cleanup containing a linked entry: $($node.FullName)"
            }
        }
        Remove-Item -LiteralPath $checked -Recurse -Force
    }
}
function Test-Player([string]$Folder) {
    return ((Test-Path -LiteralPath (Join-Path $Folder 'Parallax.exe') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Folder 'UnityPlayer.dll') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Folder 'Parallax_Data\globalgamemanagers') -PathType Leaf))
}
function Assert-NoRunningPlayer([string]$Folder) {
    $exe = Join-Path $Folder 'Parallax.exe'
    foreach ($player in @(Get-Process -Name Parallax -ErrorAction SilentlyContinue)) {
        try { $path = $player.Path } catch { $path = $null }
        if ($path -and [string]::Equals($path,$exe,[StringComparison]::OrdinalIgnoreCase)) {
            throw "Close this player before replacing it: $exe"
        }
    }
}
function File-SHA256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}
function Source-Fingerprint {
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('Parallax complete course v1')
    $lines.Add($requiredUnity)
    $lines.Add((File-SHA256 $PSCommandPath))
    foreach ($folder in $inputFolders) {
        $inputDir = Join-Path $sourceDir $folder
        if (!(Test-Path -LiteralPath $inputDir -PathType Container)) { throw "Missing source folder: $inputDir" }
        foreach ($file in (Get-ChildItem -LiteralPath $inputDir -File -Recurse | Sort-Object FullName)) {
            if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Linked source file: $($file.FullName)" }
            $relative = $file.FullName.Substring($sourceDir.Length).Replace('\','/')
            $lines.Add($relative + ':' + (File-SHA256 $file.FullName))
        }
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($lines -join [Environment]::NewLine)))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
function Find-Editor {
    $chosen = $UnityEditor
    if (!$chosen) { $chosen = $env:UNITY_EDITOR_PATH }
    if (!$chosen) {
        $choices = @()
        if ($env:LOCALAPPDATA) {
            $choices += Join-Path $env:LOCALAPPDATA "Unity\Editors\$requiredUnity\Editor\Unity.exe"
            # Desktop app installations can live in the current user's package cache.
            $packages = Join-Path $env:LOCALAPPDATA 'Packages'
            if (Test-Path -LiteralPath $packages -PathType Container) {
                foreach ($package in @(Get-ChildItem -LiteralPath $packages -Directory -Filter 'OpenAI.Codex_*' -ErrorAction SilentlyContinue)) {
                    $choices += Join-Path $package.FullName "LocalCache\Local\Unity\Editors\$requiredUnity\Editor\Unity.exe"
                }
            }
        }
        if ($env:ProgramFiles) { $choices += Join-Path $env:ProgramFiles "Unity\Hub\Editor\$requiredUnity\Editor\Unity.exe" }
        if ($env:USERPROFILE) { $choices += Join-Path $env:USERPROFILE "AppData\Local\Unity\Editors\$requiredUnity\Editor\Unity.exe" }
        $chosen = $choices | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (!$chosen -or !(Test-Path -LiteralPath $chosen -PathType Leaf)) {
        throw "Install Unity $requiredUnity, or set -UnityEditor / UNITY_EDITOR_PATH to its Unity.exe."
    }
    return (Resolve-Path -LiteralPath $chosen).Path
}
function Check-UnityLock {
    # Only the disposable build project must be closed; other Unity projects may stay open.
    $editors = @()
    try { $editors = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction Stop) } catch { }
    foreach ($editorProcess in $editors) {
        $argument = [regex]::Match([string]$editorProcess.CommandLine, '(?i)(?:^|\s)-projectPath\s+(?:"([^"]+)"|([^\s]+))')
        if (!$argument.Success) { continue }
        $openProject = $argument.Groups[1].Value
        if (!$openProject) { $openProject = $argument.Groups[2].Value }
        try { $openProject = [IO.Path]::GetFullPath($openProject).TrimEnd('\','/') } catch { continue }
        if ([string]::Equals($openProject,$buildProject.TrimEnd('\','/'),[StringComparison]::OrdinalIgnoreCase)) {
            throw "Close the Unity editor using this generated project before rebuilding: $buildProject"
        }
    }
    $path = Join-Path $buildProject 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $path) {
        try {
            $handle = [IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
            $handle.Dispose()
        } catch { throw 'The generated Unity project is still locked by another editor.' }
    }
}
function Copy-Inputs {
    Check-UnityLock
    New-Item -ItemType Directory -Path $buildProject -Force | Out-Null
    foreach ($folder in $inputFolders) {
        $from = Full-Path (Join-Path $sourceDir $folder)
        $to = Assert-Generated (Join-Path $buildProject $folder)
        if (!(Is-Within $to $buildProject) -or !(Is-Within $from $sourceDir)) { throw 'Unsafe mirror target.' }
        Assert-NoLinks $from
        foreach ($node in (Get-ChildItem -LiteralPath $from -Force -Recurse)) {
            if (($node.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Source contains a linked entry: $($node.FullName)" }
        }
        if (Test-Path -LiteralPath $to) {
            foreach ($node in (Get-ChildItem -LiteralPath $to -Force -Recurse)) {
                if (($node.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Generated mirror contains a linked entry: $($node.FullName)" }
            }
        }
        # /MIR can delete only this verified disposable subdirectory.
        & robocopy.exe $from $to /MIR /IS /XJ /R:1 /W:1 /NJH /NJS /NP /NFL /NDL | Out-Null
        if ($LASTEXITCODE -lt 0 -or $LASTEXITCODE -ge 8) { throw "Input mirror failed: $folder (robocopy $LASTEXITCODE)" }
        # Only copied timestamps change: ensure Unity notices preserved-time snapshots.
        $stamp = [DateTime]::UtcNow
        Get-ChildItem -LiteralPath $to -File -Recurse | ForEach-Object { [IO.File]::SetLastWriteTimeUtc($_.FullName,$stamp) }
    }
}
function Promote-Candidate {
    Assert-NoRunningPlayer $latest
    Assert-NoRunningPlayer $previous
    $null = Assert-Generated $candidate
    $null = Assert-Generated $latest
    $null = Assert-Generated $previous
    if (!(Test-Player $candidate)) { throw 'Candidate player is incomplete; latest remains unchanged.' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $latest),(Split-Path -Parent $previous) -Force | Out-Null
    $movedLatest = $false
    try {
        if (Test-Path -LiteralPath $latest) {
            Remove-Generated $previous
            Move-Item -LiteralPath $latest -Destination $previous
            $movedLatest = $true
        }
        Move-Item -LiteralPath $candidate -Destination $latest
    } catch {
        if ($movedLatest -and !(Test-Path -LiteralPath $latest) -and (Test-Path -LiteralPath $previous)) {
            Move-Item -LiteralPath $previous -Destination $latest
        }
        throw
    }
}

if (!(Test-Path -LiteralPath $sourceDir -PathType Container)) { throw "Missing Unity source: $sourceDir" }
Assert-NoLinks $sourceDir
$versionLine = Get-Content -LiteralPath (Join-Path $sourceDir 'ProjectSettings\ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
if (($versionLine -replace '^m_EditorVersion: ','').Trim() -ne $requiredUnity) {
    throw "This launcher expects Unity $requiredUnity; check ProjectVersion.txt."
}
$chosenRoot = $WorkspaceRoot
if (!$chosenRoot) { $chosenRoot = $env:PARALLAX_BUILD_ROOT }
$configPath = Join-Path $repoDir 'Builds\build-workspace.json'
if (!$chosenRoot -and (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if (!$config.workspaceRoot) { throw "Missing workspaceRoot in $configPath" }
    $chosenRoot = [Environment]::ExpandEnvironmentVariables([string]$config.workspaceRoot)
}
if (!$chosenRoot) { $chosenRoot = Join-Path $repoDir 'Builds' }
$workspace = Full-Path $chosenRoot
Assert-NoLinks $workspace
$buildProject = Assert-Generated (Join-Path $workspace 'StageProject')
$candidate = Assert-Generated (Join-Path $workspace 'Candidate\Tutorial')
$latest = Assert-Generated (Join-Path $workspace 'Latest\Tutorial')
$previous = Assert-Generated (Join-Path $workspace 'Previous\Tutorial')
$logs = Assert-Generated (Join-Path $workspace 'Logs\Complete')
$tempDir = Assert-Generated (Join-Path $workspace 'Temp\Complete')
New-Item -ItemType Directory -Path $workspace,$logs,$tempDir -Force | Out-Null
$lock = $null
try {
    try {
        $lock = [IO.File]::Open((Join-Path $workspace 'complete-course.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    } catch { throw 'Another complete-course build or promotion is already in progress.' }
    Write-Host 'Checking source content (including Documentation)...'
    $fingerprint = Source-Fingerprint
    $state = $null
    $statePath = Join-Path $latest 'complete-build-state.json'
    if (Test-Path -LiteralPath $statePath) {
        try { $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json } catch { $state = $null }
    }
    if ($Rebuild -or !$state -or $state.fingerprint -ne $fingerprint -or !(Test-Player $latest)) {
        $editor = Find-Editor
        Assert-NoRunningPlayer $candidate
        Assert-NoRunningPlayer $latest
        Check-UnityLock
        Copy-Inputs
        if ((Source-Fingerprint) -ne $fingerprint) { throw 'Source changed while copying; retry after saving edits.' }
        Remove-Generated $candidate
        New-Item -ItemType Directory -Path $candidate -Force | Out-Null
        $buildId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
        $logFile = Join-Path $logs ($buildId + '-unity.log')
        $arguments = @('-batchmode','-quit','-force-d3d11','-buildTarget','StandaloneWindows64',
            '-standaloneBuildSubtarget','Player','-projectPath',$buildProject,
            '-executeMethod','ShadeLink.Editor.TutorialElevatorBuild.BuildWindows',
            '-elevator-output',$candidate,'-logFile',$logFile)
        $argumentLine = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
        Write-Host "Building complete Tutorial -> Museum -> Beach player. Log: $logFile"
        $oldTemp = $env:TEMP; $oldTmp = $env:TMP
        try {
            $env:TEMP = $tempDir; $env:TMP = $tempDir
            $process = Start-Process -FilePath $editor -ArgumentList $argumentLine -WorkingDirectory $buildProject -WindowStyle Hidden -PassThru
        } finally { $env:TEMP = $oldTemp; $env:TMP = $oldTmp }
        $process.WaitForExit(); $process.Refresh()
        if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode); latest was not replaced. See $logFile" }
        $reportPath = Join-Path $candidate 'build-result.json'
        if (!(Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw "Build result missing; see $logFile" }
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if ($report.result -ne 'Succeeded' -or $report.errors -ne 0 -or !(Test-Player $candidate)) {
            throw "Build failed or is incomplete; latest was not replaced. See $logFile"
        }
        if ((Source-Fingerprint) -ne $fingerprint) { throw 'Source changed during the build; candidate was not promoted.' }
        [ordered]@{ fingerprint=$fingerprint; unity=$requiredUnity; buildId=$buildId; builtAt=[DateTime]::UtcNow.ToString('o');
            entry='Tutorial'; scenes=@('Tutorial','Gallery','Beach'); validation='Unity build/report/player-file checks; native gameplay suites are separate' } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $candidate 'complete-build-state.json') -Encoding UTF8
        Promote-Candidate
        Write-Host "Build succeeded: $(Join-Path $latest 'Parallax.exe')"
    } else { Write-Host 'Source fingerprint matches the existing complete-course player.' }
} finally { if ($lock) { $lock.Dispose() } }
if (!$BuildOnly) {
    if (!(Test-Player $latest)) { throw 'No complete successful player is available to run.' }
    $playerLog = Join-Path $logs ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-player.log')
    Start-Process -FilePath (Join-Path $latest 'Parallax.exe') -WorkingDirectory $latest -ArgumentList ('-logFile "' + $playerLog + '"') -WindowStyle Normal | Out-Null
    Write-Host "Started at Tutorial lesson 1. Player log: $playerLog"
}
