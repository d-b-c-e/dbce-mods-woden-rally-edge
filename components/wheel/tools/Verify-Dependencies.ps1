$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$toolkit = Join-Path $root 'lib\toolkit'
if ((Get-Content (Join-Path $toolkit 'VERSION') -TotalCount 1) -ne 'v0.12.0') { throw 'Unexpected toolkit pin' }
foreach ($line in Get-Content (Join-Path $toolkit 'MANIFEST.txt')) {
    if ($line -match '^([A-Fa-f0-9]{64})  (.+)$') {
        $expected = $Matches[1]; $file = Join-Path $toolkit $Matches[2]
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $expected) { throw "Toolkit artifact changed: $file" }
    }
}
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
