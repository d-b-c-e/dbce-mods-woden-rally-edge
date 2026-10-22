param([ValidatePattern("^[0-9]{2}$")][string]$Run="01")
$ErrorActionPreference='Stop'
. ./vendor/playback/Stage-RigLease.ps1
$slot=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
if(Test-Path -LiteralPath $slot){throw 'Shared slot occupied; do not replace its holder.'}
$lease=Enter-StageRigLease -Path $slot -Owner melo-woden-settings -Purpose 'source-linked UI build and fixture only; no native game device'
$start=[DateTimeOffset]::UtcNow
$buildExit=$null;$testExit=$null;$released=$false
try {
    Assert-StageRigLease -Path $slot -Token $lease.token
    & dotnet build components/wheel/tests/WodenRallyEdge.UiTests -c Release --no-restore -p:UseSharedCompilation=false -nodeReuse:false *> "artifacts/settings-parity/ui-build-$Run.log"
    $buildExit=$LASTEXITCODE
    if($buildExit -ne 0){throw "UI build failed: $buildExit"}
    & dotnet components/wheel/tests/WodenRallyEdge.UiTests/bin/Release/net10.0/WodenRallyEdge.UiTests.dll *> "artifacts/settings-parity/ui-run-$Run.log"
    $testExit=$LASTEXITCODE
    if($testExit -ne 0){throw "UI fixture failed: $testExit"}
} finally {
    $released=Exit-StageRigLease -Lease $lease
    [ordered]@{startedUtc=$start;endedUtc=[DateTimeOffset]::UtcNow;buildExit=$buildExit;testExit=$testExit;released=$released;slotAbsent=!(Test-Path -LiteralPath $slot);route='source-linked Panel.cs; fake Unity/devices; no game/native/device/force'} | ConvertTo-Json | Set-Content "artifacts/settings-parity/ui-result-$Run.json"
}
