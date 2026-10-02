param(
    [string]$StageName='ceramics',
    [string]$BackupName='source-before-ceramics',
    [string]$Variant='curved-porcelain-2-shadow-reviewed'
)
$ErrorActionPreference = 'Stop'
$ceramicStage = Join-Path $PSScriptRoot $StageName
$ceramicSource = 'C:\Users\mjnse\OneDrive\바탕 화면\Parallax_Gallery_Source'
$ceramicBackup = Join-Path $PSScriptRoot $BackupName
$ceramicManifestPath = Join-Path $ceramicSource 'SourceManifest.json'
$ceramicManifest = Get-Content -LiteralPath $ceramicManifestPath -Raw | ConvertFrom-Json -AsHashtable
$ceramicFiles = @(Get-ChildItem -LiteralPath $ceramicStage -Recurse -File)
if (Test-Path -LiteralPath $ceramicBackup) { throw 'Backup already exists; inspect before another sync.' }
foreach ($file in $ceramicFiles) {
    $relative = $file.FullName.Substring($ceramicStage.Length + 1).Replace('\','/')
    $destination = Join-Path $ceramicSource $relative
    if (Test-Path -LiteralPath $destination) {
        if (-not $ceramicManifest.files.ContainsKey($relative)) { throw "Untracked existing target: $relative" }
        $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $ceramicManifest.files[$relative]) { throw "Preserving unexpected user edits: $relative" }
    }
}
New-Item -ItemType Directory -Path $ceramicBackup | Out-Null
Copy-Item -LiteralPath $ceramicManifestPath -Destination (Join-Path $ceramicBackup 'SourceManifest.json')
foreach ($file in $ceramicFiles) {
    $relative = $file.FullName.Substring($ceramicStage.Length + 1)
    $destination = Join-Path $ceramicSource $relative
    if (Test-Path -LiteralPath $destination) {
        $backupFile = Join-Path $ceramicBackup $relative
        New-Item -ItemType Directory -Path (Split-Path $backupFile) -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backupFile
    }
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    $ceramicManifest.files[$relative.Replace('\','/')] = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Output $relative
}
$ceramicManifest['variant'] = $Variant
[IO.File]::WriteAllText($ceramicManifestPath, ($ceramicManifest | ConvertTo-Json -Depth 8))
Write-Output "Source synchronized; previous files preserved in $ceramicBackup."
