[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PlayerDirectory,
    [Parameter(Mandatory=$true)][string]$ResultDirectory,
    [string]$SourceProject,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedAssemblySha256,
    [ValidateRange(30,3600)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'This runner requires the Windows D3D11 player.' }
if (!$SourceProject) { $SourceProject = Join-Path (Split-Path -Parent $PSScriptRoot) 'projects\all-stages' }
function Full-Path([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.IndexOfAny([char[]]@(0,10,13,34)) -ge 0) {
        throw 'Paths must not be empty or contain NUL, line breaks, or double quotes.'
    }
    $full = [IO.Path]::GetFullPath($Path)
    if ($full -eq [IO.Path]::GetPathRoot($full)) { throw 'A filesystem root is not a valid task directory.' }
    return $full.TrimEnd('\','/')
}
function Is-Within([string]$Path,[string]$Root) {
    return $Path.StartsWith($Root + '\',[StringComparison]::OrdinalIgnoreCase)
}
function Overlaps([string]$Left,[string]$Right) {
    return [string]::Equals($Left,$Right,[StringComparison]::OrdinalIgnoreCase) -or
        (Is-Within $Left $Right) -or (Is-Within $Right $Left)
}
function Assert-NoLinks([string]$Path) {
    $probe = [IO.Path]::GetFullPath($Path)
    while ($probe) {
        if (Test-Path -LiteralPath $probe) {
            $item = Get-Item -LiteralPath $probe -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing a path through a junction or symlink: $probe"
            }
        }
        $parent = Split-Path -Parent $probe
        if (!$parent -or $parent -eq $probe) { break }
        $probe = $parent
    }
}
function Require-File([string]$Path) {
    Assert-NoLinks $Path
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file is missing: $Path" }
}
function File-Hash([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return '<absent>' }
    Assert-NoLinks $Path
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Expected a file: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}
function Save-Json([string]$Path,$Value) {
    $Value | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $Path -Encoding UTF8
}
function Short-Error($Record) {
    $message = [string]$Record.Exception.Message
    if ($message.Length -gt 500) { return $message.Substring(0,500) + ' [truncated]' }
    return $message
}
# Start-Process joins ArgumentList into a Windows command line. Quote every
# argument and double trailing backslashes before its closing quote. Embedded
# quotes/control characters are rejected rather than interpreted by any shell.
function Quote-Argument([string]$Value) {
    if ($Value.IndexOfAny([char[]]@(0,10,13,34)) -ge 0) { throw 'Unsafe native argument.' }
    return '"' + ($Value -replace '(\\+)$','$1$1') + '"'
}
$PlayerDirectory = Full-Path $PlayerDirectory
$ResultDirectory = Full-Path $ResultDirectory
$SourceProject = Full-Path $SourceProject
$productionDirectory = Full-Path (Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\Shade Link Team\Parallax')
foreach ($path in @($PlayerDirectory,$ResultDirectory,$SourceProject,$productionDirectory)) { Assert-NoLinks $path }
foreach ($protected in @($SourceProject,$PlayerDirectory,$productionDirectory)) {
    if (Overlaps $ResultDirectory $protected) { throw "Result directory overlaps protected source/player/storage: $protected" }
}
if (!(Test-Path -LiteralPath $SourceProject -PathType Container)) { throw "Source project is missing: $SourceProject" }
if (Test-Path -LiteralPath $ResultDirectory) { throw 'ResultDirectory must not exist; preserve previous results and choose a fresh directory.' }
$player = Join-Path $PlayerDirectory 'Parallax.exe'
$assembly = Join-Path $PlayerDirectory 'Parallax_Data\Managed\Assembly-CSharp.dll'
$teamRoles = Join-Path $SourceProject 'Documentation\Validation\ApprovedTeamRoles.json'
$audioGuards = Join-Path $SourceProject 'Documentation\Validation\AudioSourceGuards.json'
$exitTexture = Join-Path $SourceProject 'Assets\Parallax\Eclipse\Resources\Eclipse\Selected\ExitSign\Background.png'
foreach ($path in @($player,$assembly,$teamRoles,$audioGuards,$exitTexture)) { Require-File $path }
if ((File-Hash $assembly) -ine $ExpectedAssemblySha256) { throw 'ExpectedAssemblySha256 does not match the actual player DLL.' }
$storageNames = @('ParallaxCheckpoint.json','ParallaxCheckpoint.json.bak',
    'settings.json','settings.json.previous','ParallaxEndingSeen.json','ParallaxEndingSeen.json.bak',
    'ParallaxCheckpoint.admin-session.json','ParallaxCheckpoint.admin-session.json.bak')
function Storage-Snapshot {
    $snapshot = [ordered]@{}
    foreach ($name in $storageNames) {
        $path = Join-Path $productionDirectory $name
        $snapshot[$path] = File-Hash $path
    }
    return $snapshot
}
function Compare-Storage($Before) {
    foreach ($path in $Before.Keys) {
        $after = File-Hash $path
        [PSCustomObject]@{path=$path;before=$Before[$path];after=$after;unchanged=$Before[$path] -ceq $after}
    }
}
function Existing-Players {
    foreach ($process in @(Get-Process -Name Parallax -ErrorAction SilentlyContinue)) {
        $started = $null
        try { $started = $process.StartTime.ToUniversalTime().ToString('o') } catch { }
        [PSCustomObject]@{processId=$process.Id;startedUtc=$started;title=$process.MainWindowTitle}
    }
}
New-Item -ItemType Directory -Path $ResultDirectory | Out-Null
$startedUtc = [DateTime]::UtcNow.ToString('o')
$storageBefore = Storage-Snapshot
$existingBefore = @(Existing-Players)
Save-Json (Join-Path $ResultDirectory 'production-storage-before.json') $storageBefore
Save-Json (Join-Path $ResultDirectory 'existing-players-before.json') @($existingBefore)
$completed = @()
$failure = $null
$seenPids = New-Object 'System.Collections.Generic.HashSet[int]'
$specifications = @(
    [PSCustomObject]@{suite='ending';phase='exercise';report='eclipse-ending-results.json';checks=533},
    [PSCustomObject]@{suite='ending';phase='reload';report='eclipse-ending-reload-results.json';checks=26},
    [PSCustomObject]@{suite='shell';phase='exercise';report='player-shell-exercise-results.json';checks=361},
    [PSCustomObject]@{suite='shell';phase='reload';report='player-shell-reload-results.json';checks=144},
    [PSCustomObject]@{suite='gallery';phase='fall';report='gallery-clean-transition-results.json';checks=66},
    [PSCustomObject]@{suite='admin';phase='full';report='admin-section-menu-results.json';checks=1087},
    [PSCustomObject]@{suite='startup';phase='startup';report='admin-section-menu-results.json';checks=18}
)
function Invoke-NativePhase($Spec) {
    $directory = Join-Path $ResultDirectory $Spec.suite
    if (!(Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory | Out-Null }
    Assert-NoLinks $directory
    $log = Join-Path $directory ($Spec.phase + '.log')
    $reportPath = Join-Path $directory $Spec.report
    if (Test-Path -LiteralPath $reportPath) { throw "Phase report already exists: $reportPath" }
    $phaseBefore = Storage-Snapshot
    Save-Json (Join-Path $directory ($Spec.phase + '-storage-before.json')) $phaseBefore
    $arguments = @('-batchmode','-nosound','-force-d3d11')
    switch ($Spec.suite) {
        'ending' { $arguments += @('-eclipse-ending-validate',$directory,'-eclipse-ending-phase',$Spec.phase,
            '-eclipse-ending-test','-eclipse-exit-texture-source',$exitTexture,
            '-eclipse-team-role-source',$teamRoles,'-eclipse-audio-source-guard',$audioGuards) }
        'shell' { $arguments += @('-player-shell-validate',$directory,'-player-shell-phase',$Spec.phase) }
        'gallery' { $arguments += @('-gallery-clean-transition-test',$directory,
            '-game-progress-validate','-preview-validate',$directory) }
        default { $arguments += @('-admin-sections-validate',$directory,'-admin-sections-phase',$Spec.phase,
            '-game-progress-validate','-preview-validate',$directory)
            if ($Spec.suite -eq 'startup') { $arguments += @('-admin-start-section','Gallery','4') } }
    }
    $arguments += @('-logFile',$log)
    $worker = $null; $native = $null; $phaseFailure = $null; $exitCode = $null; $waitObserved = $false
    try {
        if ((File-Hash $assembly) -ine $ExpectedAssemblySha256) { throw 'Player DLL changed before a native process.' }
        $argumentLine = ($arguments | ForEach-Object { Quote-Argument ([string]$_) }) -join ' '
        $worker = Start-Process -FilePath $player -WorkingDirectory $PlayerDirectory -WindowStyle Hidden -PassThru -ArgumentList $argumentLine
        Save-Json (Join-Path $directory ($Spec.phase + '-process.json')) ([PSCustomObject]@{
            suite=$Spec.suite;phase=$Spec.phase;processId=$worker.Id;startedUtc=[DateTime]::UtcNow.ToString('o');
            assemblySha256=$ExpectedAssemblySha256;arguments=$arguments;hidden=$true;muted=$true;forceD3D11=$true})
        Write-Host "Started $($Spec.suite)/$($Spec.phase), own PID=$($worker.Id)"
        if (!$seenPids.Add($worker.Id)) { throw 'Native phases must use seven distinct process IDs.' }
        $clock = [Diagnostics.Stopwatch]::StartNew(); $nextUpdate = 30
        while (!$worker.WaitForExit(1000)) {
            if ($clock.Elapsed.TotalSeconds -ge $nextUpdate) {
                Write-Host "Waiting for own PID=$($worker.Id) to quit: $([int]$clock.Elapsed.TotalSeconds)s"
                $nextUpdate += 30
            }
            if ($clock.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
                throw "Native phase timed out after $TimeoutSeconds seconds. No process was terminated; own PID=$($worker.Id) may still be running."
            }
        }
        $waitObserved = $true; $worker.Refresh(); $exitCode = $worker.ExitCode
        if ($exitCode -ne 0) { throw "Native process exited with $exitCode; see $log" }
        Require-File $reportPath
        $native = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($key in @('passed','errors','checks','processId','captureCount','productionStorageUnchanged','graphicsDevice')) {
            if ($null -eq $native.PSObject.Properties[$key]) { throw "Native report is missing $key" }
        }
        if ($native.passed -ne $true -or @($native.errors).Count -ne 0 -or
            $native.productionStorageUnchanged -ne $true -or $native.captureCount -ne 0 -or
            $native.processId -ne $worker.Id -or $native.graphicsDevice -ne 'Direct3D11' -or
            @($native.checks).Count -ne $Spec.checks -or @($native.checks | Where-Object { $_.passed -ne $true }).Count -ne 0) {
            throw "Native report failed its process/storage/check contract: $($native.failure)"
        }
        if ($null -ne $native.PSObject.Properties['osInputCount'] -and $native.osInputCount -ne 0) { throw 'Native report recorded OS input.' }
        if ($null -ne $native.PSObject.Properties['renderedPixelsVerified'] -and $native.renderedPixelsVerified -ne $false) { throw 'This runner requires a capture-free data validation report.' }
        if ($null -ne $native.PSObject.Properties['dllSha256'] -and $native.dllSha256 -ine $ExpectedAssemblySha256) { throw 'Native report DLL hash does not match the actual player.' }
        if ($Spec.suite -eq 'admin' -or $Spec.suite -eq 'startup') {
            if ($native.normalSlotUnchanged -ne $true -or $native.sectionLoads -le 0 -or
                $native.sectionLoads -ne $native.expectedSectionLoads -or @($native.sections).Count -ne $native.expectedSectionLoads) { throw 'Administrator section coverage or normal-slot preservation failed.' }
            if ($Spec.suite -eq 'admin' -and ($native.menuMaps -ne 4 -or $native.sectionLoads -ne 14 -or $native.finalScene -ne 'MainMenu' -or $native.exitWalkTicks -le 0)) { throw 'Administrator full route did not cover all maps and real EXIT.' }
            if ($Spec.suite -eq 'startup' -and ($native.finalScene -ne 'Gallery' -or $native.sectionLoads -ne 1 -or $native.sections[0].index -ne 4)) { throw 'Startup did not reach the last Gallery section.' }
        }
        if ((File-Hash $assembly) -ine $ExpectedAssemblySha256) { throw 'Player DLL changed during a native phase.' }
    } catch { $phaseFailure = Short-Error $_ }
    $storage = @(Compare-Storage $phaseBefore)
    $storageUnchanged = @($storage | Where-Object { !$_.unchanged }).Count -eq 0
    if (!$storageUnchanged -and !$phaseFailure) { $phaseFailure = 'Real user storage changed during this phase.' }
    $phaseResult = [PSCustomObject]@{
        suite=$Spec.suite;phase=$Spec.phase;passed=(!$phaseFailure -and $storageUnchanged);
        failure=$phaseFailure;processId=if($worker){$worker.Id}else{$null};waitForExitObserved=$waitObserved;
        exitCode=$exitCode;terminatedByRunner=$false;report=$reportPath;log=$log;
        nativeChecks=if($native){@($native.checks).Count}else{0};expectedChecks=$Spec.checks;
        assemblySha256=(File-Hash $assembly);productionStorageUnchanged=$storageUnchanged;
        productionStorage=$storage;captureCount=if($native){$native.captureCount}else{$null};osInputCount=0
    }
    Save-Json (Join-Path $directory ($Spec.phase + '-summary.json')) $phaseResult
    if ($worker) { $worker.Dispose() }
    return $phaseResult
}
try {
    foreach ($spec in $specifications) {
        $phase = Invoke-NativePhase $spec
        $completed += $phase
        if (!$phase.passed) { throw "Failed $($spec.suite)/$($spec.phase): $($phase.failure)" }
        Write-Host "Completed $($spec.suite)/$($spec.phase): $($phase.nativeChecks) checks"
    }
} catch { $failure = Short-Error $_ }
$storageFinal = @(Compare-Storage $storageBefore)
$storageUnchanged = @($storageFinal | Where-Object { !$_.unchanged }).Count -eq 0
$finalAssembly = File-Hash $assembly
$summary = [PSCustomObject]@{
    passed=(!$failure -and $storageUnchanged -and $finalAssembly -ieq $ExpectedAssemblySha256 -and $completed.Count -eq 7 -and $seenPids.Count -eq 7);
    failure=$failure;startedUtc=$startedUtc;completedUtc=[DateTime]::UtcNow.ToString('o');
    playerDirectory=$PlayerDirectory;sourceProject=$SourceProject;resultDirectory=$ResultDirectory;
    expectedAssemblySha256=$ExpectedAssemblySha256;assemblySha256=$finalAssembly;
    processes=$completed;distinctProcessCount=$seenPids.Count;nativeChecks=($completed | Measure-Object nativeChecks -Sum).Sum;
    productionStorageUnchanged=$storageUnchanged;productionStorage=$storageFinal;
    existingPlayersBefore=$existingBefore;existingPlayersAfter=@(Existing-Players);
    captureCount=($completed | Measure-Object captureCount -Sum).Sum;osInputCount=0;existingUserAppsTerminated=0;
    renderedPixelsVerified=$false;subjectiveListeningVerified=$false;
    scope='Seven sequential distinct hidden muted D3D11 native processes: ending exercise/reload, shell exercise/reload, hand fall, administrator full coverage, last-Gallery startup. Explicit fixtures and real production ticks/PhysX; source-image CPU samples and glyph projection data. No screenshots, explicit rendering/readback, OS input, Unity Editor invocation, or process termination. Timed-out processes are left running and later suites are not started.'
}
Save-Json (Join-Path $ResultDirectory 'native-validation-summary.json') $summary
$summary | Select-Object passed,nativeChecks,distinctProcessCount,productionStorageUnchanged,assemblySha256,failure | ConvertTo-Json | Write-Host
if (!$summary.passed) { throw "Native validation failed. See $(Join-Path $ResultDirectory 'native-validation-summary.json')" }
