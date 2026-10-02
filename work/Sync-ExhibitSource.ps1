$ErrorActionPreference = 'Stop'
$stage = Join-Path $PSScriptRoot 'exhibits'
$sourceProject = 'C:\Users\mjnse\OneDrive\바탕 화면\Parallax_Gallery_Source'
$backup = Join-Path $PSScriptRoot 'source-before-exhibits'
$manifestPath = Join-Path $sourceProject 'SourceManifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
$files = @(Get-ChildItem -LiteralPath $stage -Recurse -File)
# Verify all pre-existing targets before changing any source file.
foreach ($file in $files) {
    $relative = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
    $destination = Join-Path $sourceProject $relative
    if (Test-Path -LiteralPath $destination) {
        if (-not $manifest.files.ContainsKey($relative)) { throw "Untracked existing target requires review: $relative" }
        $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $manifest.files[$relative]) { throw "Source changed since package creation; preserving it: $relative" }
    }
}
New-Item -ItemType Directory -Path $backup -Force | Out-Null
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $backup 'SourceManifest.json')
foreach ($file in $files) {
    $relative = $file.FullName.Substring($stage.Length + 1)
    $destination = Join-Path $sourceProject $relative
    if (Test-Path -LiteralPath $destination) {
        $backupFile = Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $backupFile) -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backupFile
    }
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    $manifest.files[$relative.Replace('\','/')] = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Output $relative
}
$manifest['variant'] = 'puzzle-exhibits-1'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 8))
Write-Output 'Source synchronized; original modified files and manifest backed up in work/source-before-exhibits.'
