#Requires -Version 7.0
# Files only; no game, registry, device, owner state or rig lease.
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Trial-Result.ps1"
$temp = Join-Path ([IO.Path]::GetTempPath()) ('woden-trial-result-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $temp 'trace')) | Out-Null
$checks=0
function Write-Case([string]$Mode='shim-applied-car-trial') {
    'restored' | Set-Content "$temp/restored.txt"
    @{status='passed';mode=$Mode} | ConvertTo-Json | Set-Content "$temp/runtime-verification.json"
    $script:case = @{reason=$(if($Mode -eq 'shim-menu-trial'){'bounded Core menu trial completed'}else{'bounded original car trial completed'});
        rows=11577;commands=0;inputVerdict='passed: native consumption';physicalOutput=$false;
        carEvidence=@{kind='new-original-not-reference-replay';nativePlateaus=23}}
}
function Check-Case([bool]$Expected, [string]$Mode='shim-applied-car-trial') {
    $script:case | ConvertTo-Json -Depth 5 | Set-Content "$temp/trace/result.json"
    $passed=$false
    try { $value=Assert-ControlTrialResult $temp $Mode; $passed=$value.status -eq 'passed' } catch { }
    if($passed -ne $Expected){throw "Wrong trial result: expected $Expected, got $passed"}
    $script:checks++
}
Write-Case; Check-Case $true # commands=0 is the observer count, not the native plateau count.
Write-Case; $case.reason='state observation failed';$case.inputVerdict='incomplete: 0 native plateaus';$case.carEvidence.nativePlateaus=0;Check-Case $false
Write-Case; $case.inputVerdict='incomplete: 22 native plateaus';Check-Case $false
Write-Case; $case.reason='stopped by command';Check-Case $false
Write-Case; $case.physicalOutput=$true;Check-Case $false
Write-Case; $case.physicalOutput='false';Check-Case $false
Write-Case; $case.carEvidence.nativePlateaus=0;Check-Case $false
Write-Case; $case.rows=0;Check-Case $false
Write-Case; @{status='failed';mode='shim-applied-car-trial'} | ConvertTo-Json | Set-Content "$temp/runtime-verification.json";Check-Case $false
Write-Case; @{status='passed';mode='shim-menu-trial'} | ConvertTo-Json | Set-Content "$temp/runtime-verification.json";Check-Case $false
Write-Case; Remove-Item -LiteralPath "$temp/restored.txt";Check-Case $false
Write-Case 'shim-menu-trial';$case.carEvidence=$null;Check-Case $true 'shim-menu-trial'
Write-Case 'shim-car-trial';Check-Case $true 'shim-car-trial'
Write-Case 'shim-force-capture';Check-Case $true 'shim-force-capture'
Write-Case 'shim-hud-ordinary'
$case.inputVerdict='passed: probe-direct-stop, 5s HUD dwell'
$case.carEvidence.recipe='forward-50pct-8s@1'
@{status='passed';stop='probe-direct-stop';sourcePins=@{'source.jsonl'=('a'*64)};caseSha256=('b'*64);observationSha256=('c'*64)} | ConvertTo-Json -Depth 4 | Set-Content "$temp/ordinary-finalizer.json"
@{status='captured'} | ConvertTo-Json | Set-Content "$temp/hud-capture-verdict.json"
[IO.Directory]::CreateDirectory("$temp/hud-burst") | Out-Null
@{process=321;startedUtc='2026-10-13T12:00:00Z'} | ConvertTo-Json | Set-Content "$temp/context.json"
'1.0`t1`tcar-recorder-stop`tprobe-direct-stop; utc=2026-10-13T12:00:15Z' | Set-Content "$temp/trace/observations.tsv"
@{schema='dbce.window-burst@1';status='complete';saved=3;process=321;processStartedUtc='2026-10-13T12:00:00Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2},@{ArrivalElapsedSeconds=0.3})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-burst/capture.json"
[IO.Directory]::CreateDirectory("$temp/hud-poststop") | Out-Null
@{schema='dbce.window-burst@1';status='complete';saved=2;process=321;processStartedUtc='2026-10-13T12:00:00Z';startedUtc='2026-10-13T12:00:16Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-poststop/capture.json"
Check-Case $true 'shim-hud-ordinary'
$case.inputVerdict='passed: native consumption';Check-Case $false 'shim-hud-ordinary'
Write-Case 'shim-hud-ordinary';$case.inputVerdict='passed: probe-direct-stop, 5s HUD dwell';$case.carEvidence.recipe='forward-50pct-8s@1'
@{schema='dbce.window-burst@1';status='complete';saved=2;process=321;processStartedUtc='2026-10-13T12:00:00Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-burst/capture.json"
Check-Case $false 'shim-hud-ordinary'
@{schema='dbce.window-burst@1';status='complete';saved=3;process=321;processStartedUtc='2026-10-13T12:00:00Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2},@{ArrivalElapsedSeconds=0.3})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-burst/capture.json"
@{schema='dbce.window-burst@1';status='complete';saved=2;process=321;processStartedUtc='2026-10-13T12:00:00Z';startedUtc='2026-10-13T12:00:14Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-poststop/capture.json"
Check-Case $false 'shim-hud-ordinary'
@{schema='dbce.window-burst@1';status='complete';saved=2;process=322;processStartedUtc='2026-10-13T12:00:00Z';startedUtc='2026-10-13T12:00:16Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-poststop/capture.json"
Check-Case $false 'shim-hud-ordinary'
if($null -ne (Assert-ControlTrialResult $temp 'raw')){throw 'Raw observer mode was reclassified.'};$checks++
if($null -ne (Assert-ControlTrialResult $temp 'shim-admission')){throw 'Admission-only mode was reclassified.'};$checks++
Write-Output "$checks trial-result checks passed. Disposable evidence: $temp"
