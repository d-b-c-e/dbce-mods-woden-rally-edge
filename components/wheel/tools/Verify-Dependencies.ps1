$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$playbackRoot = Join-Path $root '../../vendor/playback'
$playback = Get-Content (Join-Path $playbackRoot 'provenance.json') -Raw | ConvertFrom-Json
if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $playbackRoot 'Dbce.Wheel.Playback.dll')).Name -ne 'Dbce.Wheel.Playback') { throw 'Playback payload is not the expected managed assembly' }
if ($playback.sourceState -ne 'clean' -or (Get-FileHash (Join-Path $playbackRoot 'Dbce.Wheel.Playback.dll')).Hash -ne $playback.sha256 -or
    (Get-FileHash (Join-Path $playbackRoot 'Stage-Session.ps1')).Hash -ne $playback.commandSha256) { throw 'Playback artifacts differ from their source pin' }
$toolkit = Join-Path $root 'lib\toolkit'
if ((Get-Content (Join-Path $toolkit 'VERSION') -TotalCount 1) -ne 'v0.12.0') { throw 'Unexpected toolkit pin' }
foreach ($line in Get-Content (Join-Path $toolkit 'MANIFEST.txt')) {
    if ($line -match '^([A-Fa-f0-9]{64})  (.+)$') {
        $expected = $Matches[1]; $file = Join-Path $toolkit $Matches[2]
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $expected) { throw "Toolkit artifact changed: $file" }
    }
}
$recording = Get-Content (Join-Path $root 'lib\recording\provenance.json') -Raw | ConvertFrom-Json
if ((Get-FileHash (Join-Path $root 'lib\recording\Dbce.Wheel.Recording.dll')).Hash -ne $recording.dllSha256) { throw 'Recording artifact differs from separate source pin' }
$dependencies = Get-Content (Join-Path $root 'lib\local-dependencies.json') -Raw | ConvertFrom-Json
if ($dependencies.gameAssemblySha256 -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C' -or $dependencies.unity -ne '6000.3.6f1') { throw 'Unsupported interop build identity' }
foreach ($file in Get-ChildItem (Join-Path $root 'lib\core') -Filter '*.dll') {
    $loaderFile = Join-Path $root ('lib\loader\bepinex-788\BepInEx\core\' + $file.Name)
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $loaderFile).Hash) { throw "Core library differs from cached loader: $($file.Name)" }
}
foreach ($file in $dependencies.interop) {
    if ((Get-FileHash -LiteralPath (Join-Path $root ('lib\interop\' + $file.name))).Hash -ne $file.sha256) { throw "Local interop changed: $($file.name)" }
}
Write-Host 'Dependency pins and local interop hashes verified.'
