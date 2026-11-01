param(
    [string]$SourceProject = 'C:\Users\mjnse\OneDrive\바탕 화면\Parallax_Gallery_Source',
    [string]$Stage = (Join-Path $PSScriptRoot 'exhibits'),
    [string]$PlayerDirectory = (Join-Path (Split-Path $PSScriptRoot) 'Parallax_Unity_v0.8.2_Exhibits')
)
$ErrorActionPreference = 'Stop'
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$managed = Join-Path $PlayerDirectory 'Parallax_Data\Managed'
$arguments = @('/nologo', '/target:library', '/nostdlib+', '/langversion:latest', '/define:PARALLAX_EXISTING_PLAYER', '/optimize+', '/debug:portable', ('/out:' + (Join-Path $managed 'Assembly-CSharp.dll')))
foreach ($reference in Get-ChildItem -LiteralPath $managed -Filter '*.dll') {
    if ($reference.Name -ne 'Assembly-CSharp.dll') { $arguments += '/reference:' + $reference.FullName }
}
$compileSources = @()
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $SourceProject 'Assets\Parallax') -Recurse -Filter '*.cs') {
    if ($source.FullName -match '[\\/]Editor[\\/]') { continue }
    $relative = $source.FullName.Substring($SourceProject.Length + 1)
    $replacement = Join-Path $Stage $relative
    if (Test-Path -LiteralPath $replacement) { $compileSources += $replacement } else { $compileSources += $source.FullName }
}
foreach($source in Get-ChildItem -LiteralPath (Join-Path $Stage 'Assets') -Recurse -Filter '*.cs') {
    if($source.FullName -notmatch '[\\/]Editor[\\/]') { $compileSources += $source.FullName }
}
$compatibility = Join-Path $PSScriptRoot 'compile-compatibility'
New-Item -ItemType Directory -Path $compatibility -Force | Out-Null
foreach ($source in $compileSources | Select-Object -Unique) {
    # UnityLinker removes constants already inlined by the original compiler.
    # Substitute their exact C# constant values in temporary compile copies only.
    $code = [IO.File]::ReadAllText($source).Replace('Mathf.PI', '3.14159274f').Replace('Mathf.Deg2Rad', '0.0174532924f').Replace('Mathf.Rad2Deg', '57.29578f').Replace('float.PositiveInfinity', '(1f/0f)').Replace('float.NegativeInfinity', '(-1f/0f)')
    $target = Join-Path $compatibility ([IO.Path]::GetFileName($source))
    [IO.File]::WriteAllText($target, $code)
    $arguments += $target
}
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed' }
$textureFolder = Join-Path $PlayerDirectory 'Parallax_Data\StreamingAssets\GalleryExhibits'
New-Item -ItemType Directory -Path $textureFolder -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $Stage 'Assets\StreamingAssets\GalleryExhibits') -File | Where-Object Extension -in @('.png','.rgba') | Copy-Item -Destination $textureFolder
Write-Output 'Compiled source into the separate existing-player copy; this is not a Unity Editor rebuild.'
