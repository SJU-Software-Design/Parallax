[CmdletBinding()]
param(
    [ValidateSet('Gallery','Library','Beach','Starship','All')]
    [string]$Stage = 'All',
    [switch]$BuildOnly,
    [switch]$Rebuild,
    [string]$UnityEditor
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $repoDir 'projects\all-stages'
$buildRoot = Join-Path $repoDir 'Builds\Latest'
$buildProject = Join-Path $repoDir 'Builds\StageProject'
$builderFile = Join-Path $PSScriptRoot 'unity\StagePlayerBuild.cs'
$stageNames = @{ Gallery='미술관'; Library='도서관'; Beach='해변'; Starship='우주선' }

function Get-SourceFingerprint {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('Parallax stage player v1')
    foreach ($folder in @('Assets','Packages','ProjectSettings')) {
        $inputDir = Join-Path $sourceDir $folder
        if (!(Test-Path -LiteralPath $inputDir -PathType Container)) {
            throw "Unity 프로젝트 폴더가 없습니다: $inputDir"
        }
        foreach ($file in (Get-ChildItem -LiteralPath $inputDir -Recurse -File | Sort-Object FullName)) {
            $relative = $file.FullName.Substring($sourceDir.Length).Replace('\','/')
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $lines.Add($relative + ':' + $hash)
        }
    }
    $lines.Add('builder:' + (Get-FileHash -LiteralPath $builderFile -Algorithm SHA256).Hash)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Find-UnityEditor {
    param([string]$Version)
    $chosen = $UnityEditor
    if (!$chosen) { $chosen = $env:UNITY_EDITOR_PATH }
    if (!$chosen) {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA "Unity\Editors\$Version\Editor\Unity.exe"),
            (Join-Path $env:USERPROFILE "AppData\Local\Unity\Editors\$Version\Editor\Unity.exe"),
            (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$Version\Editor\Unity.exe")
        )
        $chosen = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (!$chosen -or !(Test-Path -LiteralPath $chosen -PathType Leaf)) {
        throw "Unity $Version 를 찾지 못했습니다. Unity Hub에서 설치하거나 UNITY_EDITOR_PATH에 Unity.exe 경로를 지정하세요."
    }
    return (Resolve-Path -LiteralPath $chosen).Path
}

function Copy-BuildInputs {
    New-Item -ItemType Directory -Path $buildProject -Force | Out-Null
    $unityLockPath = Join-Path $buildProject 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $unityLockPath) {
        try {
            $unityLock = [IO.File]::Open($unityLockPath, [IO.FileMode]::Open,
                [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $unityLock.Dispose()
        } catch {
            throw '이전 Unity 빌드가 아직 실행 중입니다. 빌드가 끝난 뒤 다시 실행하세요.'
        }
    }
    $safeRoot = [IO.Path]::GetFullPath($buildProject).TrimEnd('\') + '\'
    foreach ($folder in @('Assets','Packages','ProjectSettings')) {
        $from = Join-Path $sourceDir $folder
        $to = [IO.Path]::GetFullPath((Join-Path $buildProject $folder))
        # /MIR may delete stale copied files, so constrain it to the generated build project.
        if (!$to.StartsWith($safeRoot, [StringComparison]::OrdinalIgnoreCase) -or
            $to.StartsWith([IO.Path]::GetFullPath($sourceDir), [StringComparison]::OrdinalIgnoreCase)) {
            throw "빌드 복사 경로를 확인할 수 없습니다: $to"
        }
        $changedFiles = @()
        foreach ($inputFile in (Get-ChildItem -LiteralPath $from -Recurse -File)) {
            $relativePath = $inputFile.FullName.Substring($from.Length).TrimStart('\')
            $copiedPath = Join-Path $to $relativePath
            if (!(Test-Path -LiteralPath $copiedPath -PathType Leaf) -or
                (Get-FileHash -LiteralPath $inputFile.FullName -Algorithm SHA256).Hash -ne
                (Get-FileHash -LiteralPath $copiedPath -Algorithm SHA256).Hash) {
                $changedFiles += $copiedPath
            }
        }
        # /IS copies changed bytes even when source size and timestamp were preserved.
        & robocopy.exe $from $to /MIR /IS /XJ /R:2 /W:1 /NJH /NJS /NP /NFL /NDL | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "빌드용 파일 복사 실패: $from (robocopy $LASTEXITCODE)" }
        # Unity must also notice those content changes in its existing import cache.
        foreach ($copiedPath in $changedFiles) {
            [IO.File]::SetLastWriteTimeUtc($copiedPath, [DateTime]::UtcNow)
        }
    }
    $editorDir = Join-Path $buildProject 'Assets\Editor'
    New-Item -ItemType Directory -Path $editorDir -Force | Out-Null
    $destination = Join-Path $editorDir 'ParallaxStageLauncherBuild.cs'
    if (Test-Path -LiteralPath (Join-Path $sourceDir 'Assets\Editor\ParallaxStageLauncherBuild.cs')) {
        throw '빌드 도구와 같은 이름의 소스 파일이 있습니다: Assets/Editor/ParallaxStageLauncherBuild.cs'
    }
    Copy-Item -LiteralPath $builderFile -Destination $destination -Force
}

function Test-PlayerFiles {
    param([string]$OutputDir)
    return ((Test-Path -LiteralPath (Join-Path $OutputDir 'Parallax.exe') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $OutputDir 'UnityPlayer.dll') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $OutputDir 'Parallax_Data\globalgamemanagers') -PathType Leaf))
}

$lock = $null
try {
    if ($Stage -eq 'All' -and !$BuildOnly) { throw '전체 빌드는 -Stage All -BuildOnly로 실행하세요.' }
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    try {
        $lock = [IO.File]::Open((Join-Path $buildRoot 'launcher.lock'), [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    } catch { throw '다른 스테이지의 실행 준비가 진행 중입니다. 빌드가 끝난 뒤 다시 실행하세요.' }

    Write-Host '저장된 최신 소스를 확인하는 중입니다...'
    $fingerprint = Get-SourceFingerprint
    $stages = if ($Stage -eq 'All') { @('Gallery','Library','Beach','Starship') } else { @($Stage) }
    $pending = @()
    foreach ($name in $stages) {
        $outputDir = Join-Path $buildRoot $name
        $statePath = Join-Path $outputDir 'build-state.json'
        $state = $null
        if (Test-Path -LiteralPath $statePath) {
            try { $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json } catch { $state = $null }
        }
        if ($Rebuild -or !$state -or $state.fingerprint -ne $fingerprint -or
            $state.stage -ne $name -or !(Test-PlayerFiles $outputDir)) {
            $pending += $name
        } else { Write-Host "$($stageNames[$name]): 최신 실행 파일이 준비되어 있습니다." }
    }

    if ($pending.Count -gt 0) {
        $versionLine = Get-Content -LiteralPath (Join-Path $sourceDir 'ProjectSettings\ProjectVersion.txt') |
            Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
        $version = ($versionLine -replace '^m_EditorVersion: ', '').Trim()
        $editor = Find-UnityEditor $version
        foreach ($name in $pending) {
            $outputDir = Join-Path $buildRoot $name
            $exe = Join-Path $outputDir 'Parallax.exe'
            $running = Get-Process -Name Parallax -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -eq $exe }
            if ($running) { throw "$($stageNames[$name]) 게임을 먼저 종료한 뒤 다시 실행하세요." }
            New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
            $statePath = Join-Path $outputDir 'build-state.json'
            if (Test-Path -LiteralPath $statePath) { Remove-Item -LiteralPath $statePath }
        }
        Write-Host '최신 코드로 빌드합니다. 첫 빌드에는 몇 분이 걸릴 수 있습니다.'
        Copy-BuildInputs
        if ((Get-SourceFingerprint) -ne $fingerprint) {
            throw '복사 도중 소스가 변경됐습니다. 편집 내용을 저장한 뒤 다시 실행하세요.'
        }
        $buildId = [Guid]::NewGuid().ToString('N')
        $logFile = Join-Path $buildRoot 'build.log'
        $unityArgs = @('-batchmode','-nographics','-quit',
            '-buildTarget','StandaloneWindows64','-standaloneBuildSubtarget','Player',
            '-projectPath',('"' + $buildProject + '"'),
            '-executeMethod','Parallax.StageLauncher.Editor.StagePlayerBuild.Build',
            '-parallaxStages',($pending -join ','),
            '-parallaxOutput',('"' + $buildRoot + '"'),
            '-parallaxBuildId',$buildId,'-parallaxUnityVersion',$version,
            '-logFile',('"' + $logFile + '"'))
        Write-Host "빌드 로그: $logFile"
        $process = Start-Process -FilePath $editor -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Unity 빌드 실패 ($($process.ExitCode)). 로그를 확인하세요: $logFile" }
        if ((Get-SourceFingerprint) -ne $fingerprint) {
            throw '빌드 도중 소스가 변경됐습니다. 편집 내용을 저장한 뒤 다시 실행하세요.'
        }
        foreach ($name in $pending) {
            $outputDir = Join-Path $buildRoot $name
            $reportFile = Join-Path $outputDir 'build-result.json'
            if (!(Test-Path -LiteralPath $reportFile)) { throw "빌드 결과가 없습니다: $reportFile" }
            $report = Get-Content -LiteralPath $reportFile -Raw | ConvertFrom-Json
            if ($report.result -ne 'Succeeded' -or $report.buildId -ne $buildId -or
                $report.stage -ne $name -or !(Test-PlayerFiles $outputDir)) {
                throw "빌드가 완료되지 않았습니다: $name. 로그: $logFile"
            }
            [ordered]@{ stage=$name; fingerprint=$fingerprint; unity=$version;
                builtAt=[DateTime]::UtcNow.ToString('o'); buildId=$buildId } |
                ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDir 'build-state.json') -Encoding UTF8
            Write-Host "$($stageNames[$name]): 빌드 완료"
        }
    }
    $lock.Dispose()
    $lock = $null
    if (!$BuildOnly) {
        $outputDir = Join-Path $buildRoot $Stage
        Write-Host "$($stageNames[$Stage]) 실행 중..."
        # The player window is intentionally visible: this CMD is the user's game launcher.
        Start-Process -FilePath (Join-Path $outputDir 'Parallax.exe') -WorkingDirectory $outputDir |
            Out-Null
    }
} catch {
    Write-Host ("실행 준비에 실패했습니다. " + $_.Exception.Message) -ForegroundColor Red
    exit 1
} finally {
    if ($null -ne $lock) { $lock.Dispose() }
}
