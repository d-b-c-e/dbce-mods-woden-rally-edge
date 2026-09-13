$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('artifacts\installer-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $fixture | Out-Null
# Only a native file copy for the identity check. Never execute it or create a game executable.
Copy-Item -LiteralPath 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\GameAssembly.dll' -Destination $fixture
& (Join-Path $PSScriptRoot 'Install-Dev.ps1') -GameDir $fixture
$plugin = Join-Path $fixture 'BepInEx\plugins\WodenRallyEdgeWheel'
foreach ($name in 'WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','WheelFfb.dll','recording-provenance.json','telemetry-schema.json','install-receipt.json') {
    if (-not (Test-Path -LiteralPath (Join-Path $plugin $name))) { throw "Installer omitted $name" }
}
if (-not ((Get-Content (Join-Path $fixture 'BepInEx\config\BepInEx.cfg') -Raw) -match '(?m)^UnityBaseLibrariesSource\s*=\s*\r?$')) { throw 'IL2CPP workaround not seeded' }
$before = (Get-FileHash -LiteralPath (Join-Path $plugin 'WodenRallyEdgeWheel.dll')).Hash
$refused = $false
try { & (Join-Path $PSScriptRoot 'Install-Dev.ps1') -GameDir $fixture } catch { if ($_.Exception.Message -notmatch 'already installed') { throw }; $refused = $true }
if (-not $refused) { throw 'Installer overwrote existing plugin' }
if ((Get-FileHash -LiteralPath (Join-Path $plugin 'WodenRallyEdgeWheel.dll')).Hash -ne $before) { throw 'Plugin changed on refused reinstall' }
if (Test-Path -LiteralPath (Join-Path $fixture 'Super Woden Rally Edge.exe')) { throw 'Fixture unexpectedly contains a game executable' }
Write-Host "PASS: initial install layout, loader config, repeat-install refusal and existing-file preservation. Fixture: $fixture"
