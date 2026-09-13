[CmdletBinding()]
param([string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$game = [IO.Path]::GetFullPath($GameDir).TrimEnd('\')
if (Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue) { throw 'Close Woden normally before installing. The installer will not stop it.' }
if ((Get-FileHash -LiteralPath (Join-Path $game 'GameAssembly.dll')).Hash -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C') { throw 'Unsupported game build' }
& (Join-Path $PSScriptRoot 'Verify-Dependencies.ps1')
$loader = Join-Path $root 'lib\loader\bepinex-788'
if (-not (Test-Path -LiteralPath (Join-Path $loader 'winhttp.dll'))) { throw 'Run Initialize-Dependencies.ps1 first' }
if (-not (Test-Path -LiteralPath (Join-Path $root 'dist\WodenRallyEdgeWheel-0.1.0-dev.zip'))) { throw 'Run tools/Package.ps1 first' }
function Assert-UnlinkedPath([string]$path) {
    $cursor = [IO.Path]::GetFullPath($path)
    if (-not $cursor.StartsWith($game + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Outside game directory: $cursor" }
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked install path refused: $cursor" }
        $cursor = Split-Path $cursor -Parent
    }
}
$destination = Join-Path $game 'BepInEx\plugins\WodenRallyEdgeWheel'
Assert-UnlinkedPath $destination
if (Test-Path -LiteralPath $destination) { throw 'A Woden plugin is already installed. Preserve its config and review an explicit update; initial installer refuses overwrite.' }
$core = Join-Path $game 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'
$installLoader = -not (Test-Path -LiteralPath $core)
if ($installLoader) {
    foreach ($name in 'winhttp.dll','doorstop_config.ini','.doorstop_version','dotnet','BepInEx') {
        Assert-UnlinkedPath (Join-Path $game $name)
        if (Test-Path -LiteralPath (Join-Path $game $name)) { throw "Existing loader/proxy/config requires review: $name" }
    }
} else {
    if ((Get-FileHash -LiteralPath $core).Hash -ne (Get-FileHash -LiteralPath (Join-Path $loader 'BepInEx\core\BepInEx.Unity.IL2CPP.dll')).Hash) { throw 'Different existing BepInEx version; refusing loader replacement' }
    $cfg = Join-Path $game 'BepInEx\config\BepInEx.cfg'
    if (-not (Test-Path -LiteralPath $cfg) -or -not ((Get-Content $cfg -Raw) -match '(?m)^UnityBaseLibrariesSource\s*=\s*\r?$')) { throw 'Existing loader must disable UnityBaseLibrariesSource before Woden launch' }
}
$stage = Join-Path $root ('artifacts\install-' + [guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath (Join-Path $root 'dist\WodenRallyEdgeWheel-0.1.0-dev.zip') -DestinationPath $stage
$manifest = Get-Content (Join-Path $stage 'manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $file = [IO.Path]::GetFullPath((Join-Path $stage $entry.path))
    if (-not $file.StartsWith($stage + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe package manifest path' }
    if ((Get-FileHash -LiteralPath $file).Hash -ne $entry.sha256) { throw 'Package content/hash mismatch' }
}
if ($installLoader) {
    foreach ($name in 'winhttp.dll','doorstop_config.ini','.doorstop_version','dotnet','BepInEx') { Copy-Item -LiteralPath (Join-Path $loader $name) -Destination $game -Recurse }
    New-Item -ItemType Directory -Force (Join-Path $game 'BepInEx\config') | Out-Null
    "[IL2CPP]`nUnityBaseLibrariesSource = `n" | Set-Content (Join-Path $game 'BepInEx\config\BepInEx.cfg') -Encoding utf8
}
New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
Copy-Item -LiteralPath (Join-Path $stage 'BepInEx\plugins\WodenRallyEdgeWheel') -Destination $destination -Recurse
@{ installedUtc = [DateTime]::UtcNow.ToString('o'); gameDirectory = $game; installedLoader = $installLoader; packageSha256 = (Get-FileHash (Join-Path $root 'dist\WodenRallyEdgeWheel-0.1.0-dev.zip')).Hash; gameLaunched = $false } | ConvertTo-Json | Set-Content (Join-Path $destination 'install-receipt.json') -Encoding utf8
Write-Host 'Development build installed. Wheel input, bonnet view and session recording default off. No game was launched.'
