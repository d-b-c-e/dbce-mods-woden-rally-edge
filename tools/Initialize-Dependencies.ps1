[CmdletBinding()]
param(
    [string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge',
    [string]$LoaderArchive
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$name = 'BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip'
$hash = 'F4CC496BD098A0DF4164B81E3737297707F13A47C2478DBA2F60EEFAB784817A'
$gameHash = (Get-FileHash -LiteralPath (Join-Path $GameDir 'GameAssembly.dll') -Algorithm SHA256).Hash
if ($gameHash -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C') { throw 'Unsupported game build. Review metadata/version before regenerating references.' }
$cache = Join-Path $root 'lib\loader'
New-Item -ItemType Directory -Force $cache | Out-Null
$archive = Join-Path $cache $name
if ($LoaderArchive) { Copy-Item -LiteralPath $LoaderArchive -Destination $archive -Force }
if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri ('https://builds.bepinex.dev/projects/bepinex_be/788/' + [uri]::EscapeDataString($name)) -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $hash) { throw 'Loader archive hash mismatch' }
$loader = Join-Path $cache 'bepinex-788'
if (-not (Test-Path -LiteralPath $loader)) { Expand-Archive -LiteralPath $archive -DestinationPath $loader }
New-Item -ItemType Directory -Force (Join-Path $root 'lib\core'), (Join-Path $root 'lib\generator-runtime') | Out-Null
Copy-Item (Join-Path $loader 'BepInEx\core\*.dll') (Join-Path $root 'lib\core') -Force
Copy-Item -LiteralPath (Join-Path $loader 'dotnet\Microsoft.Extensions.Logging.Abstractions.dll') -Destination (Join-Path $root 'lib\generator-runtime') -Force
$interop = Join-Path $root 'lib\interop'
$existingInterop = Test-Path -LiteralPath (Join-Path $interop 'Assembly-CSharp.dll')
if ($existingInterop) {
    $previous = Get-Content (Join-Path $root 'lib\local-dependencies.json') -Raw | ConvertFrom-Json
    if ($previous.gameAssemblySha256 -ne $gameHash) { throw 'Interop manifest belongs to a different game build' }
    foreach ($entry in $previous.interop) {
        if ((Get-FileHash -LiteralPath (Join-Path $interop $entry.name)).Hash -ne $entry.sha256) { throw 'Existing interop differs from its manifest' }
    }
} else {
    & dotnet run --project (Join-Path $root 'tools\InteropGenerator') -c Release -- $GameDir 6000.3.6f1 $interop
    if ($LASTEXITCODE -ne 0) { throw 'Interop generation failed. Preserve failed output for inspection; use a fresh output directory before retrying.' }
}
@{ loader = $name; loaderSha256 = $hash; gameAssemblySha256 = $gameHash; unity = '6000.3.6f1'; metadataVersion = 39; generatedWithoutUnityUnstripping = $true; interop = @(Get-ChildItem $interop -Filter '*.dll' | Sort-Object Name | ForEach-Object { @{name=$_.Name; sha256=(Get-FileHash $_.FullName).Hash} }) } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'lib\local-dependencies.json') -Encoding utf8
Write-Host 'Local build dependencies ready. Game files were read only; no loader was installed and no game was launched.'
