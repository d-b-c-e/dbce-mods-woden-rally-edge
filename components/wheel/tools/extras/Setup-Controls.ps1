#Requires -Version 5.1
<# Optional, private: set up Super Woden: Rally Edge's shared wheel controls without Wheelkit, using the packaged DBCE mod
   setup helper. It writes only previewed changes (BepInEx\config\dbce-controls.ini and [Input] Backend = Shim, read at the
   game's next start, plus screen measurements or mod settings only as changed on those tabs). It does not launch the
   game, change the Windows display mode or send wheel forces. Install the private shim package first (Install.bat). The
   helper runs from this package folder and is never copied into the game folder. #>
[CmdletBinding()]
param([string]$GameDir, [switch]$PlanOnly)
$ErrorActionPreference = 'Stop'
$gameName = 'Super Woden Rally Edge'
$packageRoot = (Split-Path $PSScriptRoot -Parent).TrimEnd('\')
$helperRoot = Join-Path $PSScriptRoot 'ModSetup'
$helper = Join-Path $helperRoot 'DbceModSetup.exe'
if (-not (Test-Path -LiteralPath (Join-Path $helperRoot 'bundle-manifest.json') -PathType Leaf)) { throw 'This package has no controls setup helper.' }
function Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
# Only the packaged, unmodified bundle runs. The executable is a .NET host, so its own hash does not identify the managed
# code beside it: every package file is checked against manifest.json, as Install checks it, then every helper file
# against the pinned bundle manifest, and nothing unlisted may sit beside them.
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $file = [IO.Path]::GetFullPath((Join-Path $packageRoot $entry.path))
    if (-not $file.StartsWith($packageRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $file -PathType Leaf) -or
        $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or (Hash $file) -ne $entry.sha256) { throw "Package integrity check failed: $($entry.path)" }
}
$pin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'standalone-setup.json') -Raw | ConvertFrom-Json
$bundlePath = Join-Path $helperRoot 'bundle-manifest.json'
if ((Hash $bundlePath) -ne $pin.bundleManifestSha256) { throw 'The setup helper bundle manifest differs from its pin.' }
$bundle = Get-Content -LiteralPath $bundlePath -Raw | ConvertFrom-Json
foreach ($file in $bundle.files) {
    $path = Join-Path $helperRoot $file.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Hash $path) -ne $file.sha256) { throw "The setup helper differs from its bundle manifest: $($file.path)" }
}
if (@(Get-ChildItem -LiteralPath $helperRoot -Recurse -File).Count -ne @($bundle.files).Count + 1) { throw 'The setup helper folder holds a file its bundle manifest does not list.' }
if (-not $GameDir) {
    # The installer's own Steam library search, without its folder dialog.
    $libraries = New-Object System.Collections.Generic.List[string]
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') {
        $value = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($value.SteamPath) { $libraries.Add($value.SteamPath) }
        if ($value.InstallPath) { $libraries.Add($value.InstallPath) }
    }
    foreach ($steam in @($libraries.ToArray())) {
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) { $libraries.Add($match.Groups[1].Value.Replace('\\', '\')) }
        }
    }
    $found = @($libraries | Select-Object -Unique | ForEach-Object { Join-Path $_ ('steamapps\common\' + $gameName) } | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'GameAssembly.dll') })
    if ($found.Count -ne 1) { throw 'The game folder was not found in one Steam library. Pass -GameDir with the Super Woden Rally Edge folder.' }
    $GameDir = $found[0]
}
$GameDir = [IO.Path]::GetFullPath($GameDir).TrimEnd('\')
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\WodenWheel-install.json') -PathType Leaf)) { throw 'Install the mod first (Install.bat), then set up controls.' }
if (Get-Process -Name $gameName -ErrorAction SilentlyContinue) { throw "Close $gameName, then set up controls. No process will be stopped." }
# The helper's own game-locked launch (Start-ModSetup.ps1 in the bundle), without needing PowerShell 7.
$arguments = '--game super-woden-rally-edge --game-dir "' + $GameDir + '"'
if ($PlanOnly) { Write-Output ('PLAN ONLY: "' + $helper + '" ' + $arguments); return }
Write-Host 'Opening setup for Super Woden: Rally Edge. Choose Preview, then Apply on each tab you use. Only previewed changes are written; the mod reads them at its next start.'
$process = Start-Process -FilePath $helper -ArgumentList $arguments -WorkingDirectory $helperRoot -PassThru -Wait
exit $process.ExitCode
