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
Write-Case 'shim-applied-forward-capture'
$case.carEvidence.nativePlateaus=3
$case.carEvidence.recipe='applied-forward-50pct-8s@1'
$case.carEvidence.forceCapture=@{forwardGroundedSeconds=2.5;qualifiedRows=12}
Check-Case $true 'shim-applied-forward-capture'
$case.carEvidence.forceCapture.qualifiedRows=0;Check-Case $false 'shim-applied-forward-capture'
$case.carEvidence.forceCapture.qualifiedRows=12;$case.carEvidence.forceCapture.forwardGroundedSeconds=1.9;Check-Case $false 'shim-applied-forward-capture'
$case.carEvidence.forceCapture.forwardGroundedSeconds=2.5;$case.carEvidence.recipe='six-actions@1';Check-Case $false 'shim-applied-forward-capture'
Write-Case 'shim-applied-forward-ordinary'
$case.reason='bounded automated forward input complete; ordinary recorder remains active'
$case.carEvidence.nativePlateaus=3
$case.carEvidence.recipe='applied-forward-50pct-8s@1'
$case.carEvidence.forceCapture=@{forwardGroundedSeconds=2.5;qualifiedRows=12}
$archive=Join-Path $temp 'ordinary-recording-original'
[IO.Directory]::CreateDirectory((Join-Path $archive 'control')) | Out-Null
$entries=@(foreach($name in @('source.jsonl','case.json','force-observation.jsonl','control/finalization.json')){
    $path=Join-Path $archive $name
    "fixture $name" | Set-Content -LiteralPath $path
    @{path=$name;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
})
$saved=@{status='saved-while-open';inputOrigin='automated diagnostic';gameAlive=$true;companionExit=0;
    sourceSha256=$entries[0].sha256;caseSha256=$entries[1].sha256;observationSha256=$entries[2].sha256;
    wheelkitState='Saved — signal analysis only';wheelkitOrigin='Input source unspecified';archive=$archive;archiveFiles=$entries}
function Write-SavedFixture { $saved | ConvertTo-Json -Depth 5 | Set-Content "$temp/ordinary-saved-result.json" }
Write-SavedFixture;Check-Case $true 'shim-applied-forward-ordinary'
$saved.gameAlive=$false;Write-SavedFixture;Check-Case $false 'shim-applied-forward-ordinary';$saved.gameAlive=$true
$saved.wheelkitOrigin='Human-driven';Write-SavedFixture;Check-Case $false 'shim-applied-forward-ordinary';$saved.wheelkitOrigin='Input source unspecified'
$saved.archiveFiles=@($entries | Where-Object {$_.path -ne 'case.json'});Write-SavedFixture;Check-Case $false 'shim-applied-forward-ordinary';$saved.archiveFiles=$entries
'tampered' | Add-Content -LiteralPath (Join-Path $archive 'source.jsonl')
Write-SavedFixture;Check-Case $false 'shim-applied-forward-ordinary'
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
Write-Case 'shim-hud-stop-only'
$case.reason='bounded menu recorder stop and dwell completed'
$case.inputVerdict='passed: 2s menu Recording, probe-direct Stop, 5s Completed/DataWritten dwell; no injected input or physical output'
$case.carEvidence=$null
@{status='refused-no-driving';stop='probe-direct-stop';sourcePins=@{'source.jsonl'=('a'*64);'force-config.json'=('b'*64);'capture-profile.json'=('c'*64)};reprocessExit=1;reason='Insufficient driving coverage: 0 samples over 0.000s'} | ConvertTo-Json -Depth 4 | Set-Content "$temp/ordinary-finalizer.json"
@{status='captured'} | ConvertTo-Json | Set-Content "$temp/hud-capture-verdict.json"
'1.0`t1`thud-recorder-stop`tprobe-direct-stop; utc=2026-10-13T12:00:15Z' | Set-Content "$temp/trace/observations.tsv"
@{schema='dbce.window-burst@1';status='complete';saved=2;process=321;processStartedUtc='2026-10-13T12:00:00Z';startedUtc='2026-10-13T12:00:16Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-poststop/capture.json"
Check-Case $true 'shim-hud-stop-only'
$case.reason='game stopping';Check-Case $false 'shim-hud-stop-only'
$case.reason='bounded menu recorder stop and dwell completed'
$case.carEvidence=@{nativePlateaus=1};Check-Case $false 'shim-hud-stop-only'
$case.carEvidence=$null
@{status='refused-no-driving';stop='probe-direct-stop';sourcePins=@{'source.jsonl'=('a'*64);'force-config.json'=('b'*64);'capture-profile.json'=('c'*64)};reprocessExit=1;reason='Recording metadata differs'} | ConvertTo-Json -Depth 4 | Set-Content "$temp/ordinary-finalizer.json"
Check-Case $false 'shim-hud-stop-only'
@{status='passed';stop='probe-direct-stop';sourcePins=@{'source.jsonl'=('a'*64);'force-config.json'=('b'*64);'capture-profile.json'=('c'*64)};caseSha256=('d'*64);observationSha256=('e'*64)} | ConvertTo-Json -Depth 4 | Set-Content "$temp/ordinary-finalizer.json"
Check-Case $true 'shim-hud-stop-only'
@{schema='dbce.window-burst@1';status='complete';saved=2;process=322;processStartedUtc='2026-10-13T12:00:00Z';startedUtc='2026-10-13T12:00:16Z';frames=@(@{ArrivalElapsedSeconds=0.1},@{ArrivalElapsedSeconds=0.2})} | ConvertTo-Json -Depth 4 | Set-Content "$temp/hud-poststop/capture.json"
Check-Case $false 'shim-hud-stop-only'
if($null -ne (Assert-ControlTrialResult $temp 'raw')){throw 'Raw observer mode was reclassified.'};$checks++
if($null -ne (Assert-ControlTrialResult $temp 'shim-admission')){throw 'Admission-only mode was reclassified.'};$checks++
Write-Output "$checks trial-result checks passed. Disposable evidence: $temp"
