#Requires -Version 7.0
<# Private shim packages only: stages the pinned, unmodified DBCE mod setup helper under Extras\ModSetup, its
   game-locked launcher and the player section of the README (the DRIVE/Art pattern, sana 89513). It is never part of
   the installed payload: Manage-Install copies only its named plugin files. Run it before the package manifest is
   written, so the installer's package check hashes every helper file too. #>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Stage, [string]$Cache = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$pinPath = Join-Path $PSScriptRoot 'standalone-setup.json'
$pin = Get-Content -LiteralPath $pinPath -Raw | ConvertFrom-Json
if (-not $Cache) { $Cache = Join-Path $root 'lib\cache' }
$zip = Join-Path $Cache $pin.zip
if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) { throw "Copy $($pin.zip) and its .sha256 sidecar from the Wheelkit artifacts into $Cache first." }
if ((Get-FileHash -LiteralPath $zip).Hash -ne $pin.zipSha256 -or
    (Get-Content -LiteralPath "$zip.sha256" -Raw).TrimEnd("`r", "`n") -cne ($pin.zipSha256 + '  ' + $pin.zip)) { throw 'Setup helper ZIP or its sidecar differs from the pin.' }
$extras = Join-Path $Stage 'Extras'
if (Test-Path -LiteralPath $extras) { throw 'The stage already has an Extras folder.' }
$helper = Join-Path $extras 'ModSetup'
Expand-Archive -LiteralPath $zip -DestinationPath $helper
$bundlePath = Join-Path $helper 'bundle-manifest.json'
if ((Get-FileHash -LiteralPath $bundlePath).Hash -ne $pin.bundleManifestSha256) { throw 'Setup helper bundle manifest differs from the pin.' }
$bundle = Get-Content -LiteralPath $bundlePath -Raw | ConvertFrom-Json
if (@($bundle.files).Count -ne $pin.files -or @(Get-ChildItem -LiteralPath $helper -Recurse -File).Count -ne $pin.files + 1) { throw 'Extracted setup helper file count differs from the pin.' }
foreach ($file in $bundle.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $helper $file.path)).Hash -ne $file.sha256) { throw "Extracted setup helper differs from its bundle manifest: $($file.path)" }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'extras\Setup-Controls.ps1') -Destination $extras
Copy-Item -LiteralPath $pinPath -Destination $extras
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'extras\Set Up Controls.bat') -Destination $Stage
Add-Content -LiteralPath (Join-Path $Stage 'README.md') -Value ("`n" + (Get-Content -LiteralPath (Join-Path $root 'docs\PLAYER-CONTROLS-SETUP.md') -Raw))
Write-Host "Setup helper $($pin.sourceCommit.Substring(0, 12)) staged under Extras\ModSetup."
