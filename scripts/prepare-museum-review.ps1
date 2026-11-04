[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$SourceProject,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [string]$DestinationRepository
)
$ErrorActionPreference = 'Stop'
if (!$DestinationRepository) { $DestinationRepository = Split-Path -Parent $PSScriptRoot }

function Get-ReviewSha256 {
    param([string]$LiteralPath)
    # .NET hashing stays read-only under -WhatIf in Windows PowerShell 5.1.
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($LiteralPath)
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    } finally { $stream.Dispose(); $algorithm.Dispose() }
}

$sourceRoot = [IO.Path]::GetFullPath($SourceProject).TrimEnd('\', '/')
$destinationRoot = [IO.Path]::GetFullPath($DestinationRepository).TrimEnd('\', '/')
$destinationProject = [IO.Path]::GetFullPath((Join-Path $destinationRoot 'projects\all-stages'))
if (!(Test-Path -LiteralPath (Join-Path $sourceRoot 'Assets\Parallax\Gallery') -PathType Container)) {
    throw 'SourceProject must contain Assets/Parallax/Gallery.'
}
if (!(Test-Path -LiteralPath (Join-Path $destinationRoot '.git'))) {
    throw 'DestinationRepository must be a Git repository or worktree.'
}
if (!(Test-Path -LiteralPath $destinationProject -PathType Container)) {
    throw 'DestinationRepository must contain projects/all-stages.'
}
if ($sourceRoot.Equals($destinationProject, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Source and destination projects must be different.'
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schema -ne 'parallax-museum-review-v1' -or !$manifest.valid) {
    throw 'The manifest is not a valid completed museum review manifest.'
}
if (!$manifest.reviewFiles -or $manifest.reviewFiles.Count -eq 0) {
    throw 'The manifest has no Gallery files.'
}
$sourcePrefix = $sourceRoot + [IO.Path]::DirectorySeparatorChar
$destinationPrefix = $destinationProject.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$plan = New-Object System.Collections.Generic.List[object]
$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

# Validate every input and hash before copying any file.
foreach ($entry in $manifest.reviewFiles) {
    $relative = ([string]$entry.path).Replace('\', '/')
    if ($relative -ne 'Assets/Parallax/Gallery.meta' -and !$relative.StartsWith('Assets/Parallax/Gallery/', [StringComparison]::Ordinal)) {
        throw "Manifest path is outside the Gallery scope: $relative"
    }
    if ($relative -match '(^|/)\.\.?(/|$)|:' -or !$seen.Add($relative)) {
        throw "Invalid or duplicate manifest path: $relative"
    }
    $native = $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
    $source = [IO.Path]::GetFullPath((Join-Path $sourceRoot $native))
    $destination = [IO.Path]::GetFullPath((Join-Path $destinationProject $native))
    if (!$source.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        !$destination.StartsWith($destinationPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Resolved path escapes its project: $relative"
    }
    $file = Get-Item -LiteralPath $source
    # OneDrive cloud placeholders can have ReparsePoint while remaining regular
    # files (LinkType/Target are empty). Reject actual links, not cloud storage.
    if ($file.PSIsContainer -or $file.LinkType -or $file.Target) {
        throw "Manifest input must be a regular file: $relative"
    }
    if ($file.Length -ne [long]$entry.bytes -or
        (Get-ReviewSha256 -LiteralPath $source) -ne ([string]$entry.sha256).ToLowerInvariant()) {
        throw "Source changed after manifest generation; regenerate it before copying: $relative"
    }
    $plan.Add([pscustomobject]@{ Source = $source; Destination = $destination; Path = $relative; Bytes = $file.Length })
}

$total = [long](($plan | Measure-Object Bytes -Sum).Sum)
Write-Host ("Validated {0} Gallery files ({1:N1} MiB), version {2}." -f $plan.Count, ($total / 1MB), $manifest.version)
if ($PSCmdlet.ShouldProcess($destinationProject, ("Copy {0} verified Gallery files" -f $plan.Count))) {
    foreach ($item in $plan) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($item.Destination)) | Out-Null
        Copy-Item -LiteralPath $item.Source -Destination $item.Destination -Force
    }
    Write-Host 'Gallery review files copied. Inspect the diff before staging.'
}
