# Build a local player candidate. Never installs, launches, tags or publishes.
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9.-]*$')][string]$ArtifactLabel)
$ErrorActionPreference='Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$wheel = Join-Path $root 'components/wheel'
$source = & git -C $root rev-parse HEAD
if ($LASTEXITCODE) { throw 'Cannot identify source.' }
$dirty = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -or $dirty.Count) { throw 'Commit reviewed changes before freezing a candidate.' }
$version = ([xml](Get-Content (Join-Path $wheel 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$identity = "$version-$ArtifactLabel"
$zip = Join-Path $wheel "dist/WodenRallyEdge-$identity.zip"
$stage = Join-Path $wheel "dist/player-$identity"
foreach ($path in @($zip,$zip+'.sha256',$zip+'.json',$stage)) {
    if (Test-Path -LiteralPath $path) { throw "Immutable output already exists: $path" }
}
& (Join-Path $wheel 'tools/Package.ps1') -ArtifactLabel $ArtifactLabel
Expand-Archive -LiteralPath (Join-Path $wheel "dist/WodenRallyEdgeWheel-$identity-dev.zip") -DestinationPath $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'player/QUICK-START.md') -Destination (Join-Path $stage 'README.md') -Force
@'
DBCE Woden Rally Edge mod: MIT; see LICENSE.
Dbce.Wheel.Ffb, Telemetry, Recording, Playback and WheelFfb: MIT, d-b-c-e.
Sources: https://github.com/d-b-c-e/dbce-wheel-mod-toolkit
Exact runtime pins are retained beside the plugin (toolkit.version and provenance JSON).
No game assemblies, generated interop, owner settings or recordings are included.

On first install Manage-Install.ps1 downloads BepInEx IL2CPP build 788
(6.0.0-be.788+5b766a3) from the project's official build service and verifies
its pinned SHA-256. BepInEx and its dependencies retain their own licenses.
The loader archive is not redistributed in this mod ZIP.
BepInEx source/licenses: https://github.com/BepInEx/BepInEx/tree/5b766a3
The exact supported loader archive can instead be supplied via -LoaderArchive.
'@ | Set-Content -LiteralPath (Join-Path $stage 'THIRD_PARTY_NOTICES.txt') -Encoding utf8
$manifest = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object Name -ne 'manifest.json' | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
})
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
if ((& git -C $root rev-parse HEAD) -ne $source -or @(& git -C $root status --porcelain).Count) { throw 'Source changed during preparation.' }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$hash = (Get-FileHash -LiteralPath $zip).Hash
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath ($zip+'.sha256') -Encoding ascii
[ordered]@{schema=1;version=$version;identity=$identity;source=$source;stage=$stage;archive=$zip;sha256=$hash;published=$false;installed=$false} |
    ConvertTo-Json | Set-Content -LiteralPath ($zip+'.json') -Encoding utf8
Write-Output "Prepared player candidate: $zip"
Write-Output "Run exact-package PowerShell 5.1 installer checks against: $stage"
