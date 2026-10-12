#Requires -Version 7.0
param([Parameter(Mandatory)][string]$Profile,[string]$Original='E:/Source/games/dbce-mods-woden-rally-edge/results/raw-controls-20261010-07',[string]$ShimRepo='E:/Source/research/.worktrees/art-shim-bridge',[switch]$ForceCapture)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$pin='f49cfc559f065b778967a7c94068a75df538ec69'
$out=Join-Path $repo ('artifacts/core-car-fixture-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($out)|Out-Null
$Profile=(Resolve-Path -LiteralPath $Profile).Path
$before=(Get-FileHash -LiteralPath $Profile).Hash
$recipe=@();if($ForceCapture){$recipe=@('forward')}
& dotnet run --project "$PSScriptRoot/ProtocolTests" -c Release -- $Profile "$Original/apply/profile.json" "$Original/raw-workload.json" "$out/batches.txt" @recipe
if($LASTEXITCODE){throw 'Independent workload/source validation failed.'}
& git -C $ShimRepo archive --format=zip "--output=$out/pinned.zip" $pin windows/shim windows/vendor/controls windows/tests/shim_fake_provider.hpp windows/shim_dll/dbce_shim.h
if($LASTEXITCODE){throw 'Exact provisional source pin unavailable.'}
Expand-Archive -LiteralPath "$out/pinned.zip" -DestinationPath "$out/pinned"
$src="$out/pinned/windows"
& g++ -std=c++17 -O0 -I "$src/shim" -I "$src/vendor/controls" -I "$src/tests" -I "$src/shim_dll" "$PSScriptRoot/CoreCarFixture.cpp" "$src/shim/core.cpp" "$src/shim/parse.cpp" -o "$out/core-car.exe"
if($LASTEXITCODE){throw 'Pure Core car fixture compilation failed.'}
& "$out/core-car.exe" $Profile "$out/batches.txt" "$out/frames.bin" @recipe
if($LASTEXITCODE){throw 'Actual raw Core fixture failed.'}
if((Get-FileHash -LiteralPath $Profile).Hash -ne $before){throw 'Read-only profile changed.'}
$env:WODEN_CORE_CAR_FRAMES="$out/frames.bin";$env:WODEN_CORE_MENU_PROFILE=$Profile;$env:WODEN_CAR_ORIGINAL=$Original
if($ForceCapture){$env:WODEN_CAR_RECIPE='forward'}
try {
    & dotnet run --project "$repo/components/wheel/tests/WodenRallyEdge.UiTests" -c Release;if($LASTEXITCODE){throw 'Production car consumer fixture failed.'}
    & dotnet run --project "$PSScriptRoot/LifecycleTests" -c Release;if($LASTEXITCODE){throw 'Production car lifecycle fixture failed.'}
}
finally {Remove-Item Env:WODEN_CORE_CAR_FRAMES;Remove-Item Env:WODEN_CORE_MENU_PROFILE;Remove-Item Env:WODEN_CAR_ORIGINAL;if($ForceCapture){Remove-Item Env:WODEN_CAR_RECIPE}}
[ordered]@{sourcePin=$pin;profileSha256=$before;framesSha256=(Get-FileHash "$out/frames.bin").Hash;frames=$(if($ForceCapture){1260}else{2640});recipe=$(if($ForceCapture){'forward-50pct-8s@1'}else{'six-actions@1'});devices='fake';gameLaunched=$false}|ConvertTo-Json|Set-Content "$out/receipt.json"
Write-Output "Evidence: $out"
