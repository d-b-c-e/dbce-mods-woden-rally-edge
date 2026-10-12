#Requires -Version 7.0
param([Parameter(Mandatory)][string]$Profile,[string]$ShimRepo='E:/Source/research/.worktrees/art-shim-bridge')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$pin='f49cfc559f065b778967a7c94068a75df538ec69'
$out=Join-Path $repo ('artifacts/core-menu-fixture-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($out)|Out-Null
& git -C $ShimRepo archive --format=zip "--output=$out/pinned.zip" $pin windows/shim windows/vendor/controls windows/tests/shim_fake_provider.hpp windows/shim_dll/dbce_shim.h
if($LASTEXITCODE){throw 'Exact provisional source pin unavailable.'}
Expand-Archive -LiteralPath "$out/pinned.zip" -DestinationPath "$out/pinned"
$src="$out/pinned/windows"
& g++ -std=c++17 -O0 -I "$src/shim" -I "$src/vendor/controls" -I "$src/tests" -I "$src/shim_dll" "$PSScriptRoot/CoreMenuFixture.cpp" "$src/shim/core.cpp" "$src/shim/parse.cpp" -o "$out/core-menu.exe"
if($LASTEXITCODE){throw 'Pure Core fixture compilation failed.'}
$Profile=(Resolve-Path -LiteralPath $Profile).Path
$before=(Get-FileHash -LiteralPath $Profile -Algorithm SHA256).Hash
& "$out/core-menu.exe" $Profile "$out/frames.bin"
if($LASTEXITCODE){throw 'Actual Core fixture failed.'}
if((Get-FileHash -LiteralPath $Profile -Algorithm SHA256).Hash -ne $before){throw 'Read-only profile changed.'}
$env:WODEN_CORE_MENU_FRAMES="$out/frames.bin"
$env:WODEN_CORE_MENU_PROFILE=$Profile
try { & dotnet run --project "$repo/components/wheel/tests/WodenRallyEdge.UiTests" -c Release;if($LASTEXITCODE){throw 'Production consumer fixture failed.'} }
finally {Remove-Item Env:WODEN_CORE_MENU_FRAMES;Remove-Item Env:WODEN_CORE_MENU_PROFILE}
[ordered]@{sourcePin=$pin;profileSha256=$before;framesSha256=(Get-FileHash "$out/frames.bin" -Algorithm SHA256).Hash;frames=500;devices='fake';gameLaunched=$false}|ConvertTo-Json|Set-Content "$out/receipt.json"
Write-Output "Evidence: $out"
