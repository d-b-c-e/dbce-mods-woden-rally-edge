#Requires -Version 7.5
# Actual consumer + frozen shared sequence, simulated callbacks/files only.
param([string]$SharedRoot='E:/Source/toolkits/.worktrees/zemu-launch-focus-shared-20261017/tools/playback')
$ErrorActionPreference='Stop'
foreach($pin in @(
    @('Stage-GameSession.ps1','5642E42B1B7110F33CF2E05AE1780E578D6FFDDC5A52B2E56D6AC25E520896C9'),
    @('Stage-GameForeground.ps1','D7261290CDA14A7BF4C358E87E1F9A17D08DBA419F2DF2AD1C2FF86E0823F750'),
    @('Stage-ProcessIdentity.ps1','BE132A68893EE591DD76DE7143D41F5B87C767FEA6EB628952ABFEF477713A29'))){
    if((Get-FileHash -LiteralPath (Join-Path $SharedRoot $pin[0])).Hash -cne $pin[1]){throw 'Shared fixture pin differs'}
}
. (Join-Path $SharedRoot 'Stage-GameSession.ps1')
. "$PSScriptRoot/OwnerFiles.ps1"
. "$PSScriptRoot/WodenGameSession.ps1"
# Replace only the native query seam: no user32 load, OS query, focus or input.
function Get-StageGameWindowState {
    param($Client)
    [pscustomobject]@{processId=$Client.processId;startedUtcTicks=$Client.startedUtcTicks;executablePath=$Client.executablePath;
        window=$Client.window;class=$Client.class;visible=$true;minimized=$false;
        foregroundWindow=$(if($fake.front){$Client.window}else{100});foregroundProcessId=$(if($fake.front){$Client.processId}else{20})}
}
$count=0
function Check([bool]$Value,[string]$Why){if(!$Value){throw $Why};$script:count++}
$root=Join-Path ([IO.Path]::GetTempPath()) ('woden-session-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
try{
    $identity=[pscustomobject]@{processId=42;startedUtcTicks=[DateTime]::UtcNow.AddMinutes(-1).Ticks;executablePath='C:/fixture/game.exe'}
    foreach($mode in @('good','bad-ack','late-arm','missing-neutral','stale-game-focus','stop-error','replacement','unknown-birth','focus-lost-arm')){
        $case=Join-Path $root $mode;$probe=Join-Path $case 'probe';$trace=Join-Path $probe 'trace'
        [IO.Directory]::CreateDirectory($trace)|Out-Null
        $id=[guid]::NewGuid();$nonce=$id.ToString('N');$request=Join-Path $case 'launch.json'
        Write-WodenSessionJson $request @{Id=$id.ToString()}
        $script:fake=[pscustomobject]@{time=0;front=$false;stopCalls=0;restores=0;plays=0;mode=$mode;trace=$trace;probe=$probe;nonce=$nonce;companionStopped=$false;armSeen=$false}
        $fixtureClient=[pscustomobject]@{state='selected';processId=42;startedUtcTicks=$identity.startedUtcTicks;executablePath=$identity.executablePath;window=99;class='UnityWndClass'}
        Write-WodenSessionJson (Join-Path $trace 'game-focus.json') @{processId=42;startedUtcTicks=$identity.startedUtcTicks;focused=$true;checkedUtc=[DateTime]::UtcNow.AddSeconds($(if($mode -ceq 'stale-game-focus'){-5}else{0})).ToString('O')}
        $wait={
            $fake.time++
            if(Test-Path -LiteralPath (Join-Path $case 'foreground-request.json')){$fake.front=$true}
            $cmd=Read-WodenSessionJson (Join-Path $fake.trace 'command.json')
            if($cmd -and $cmd.operation -ceq 'arm'){
                $fake.armSeen=$true
                if($fake.mode -cne 'late-arm'){
                    Write-WodenSessionJson (Join-Path $fake.trace 'reply.json') @{sequence=1;nonce=$(if($fake.mode -ceq 'bad-ack'){('b'*32)}else{$fake.nonce});armSequence=1;ok=$true;armed=$true;reply='armed'}
                    if($fake.mode -ceq 'focus-lost-arm'){$fake.front=$false}
                }
            }
            if((Test-Path -LiteralPath (Join-Path $fake.probe 'revoke.json')) -and $fake.mode -cne 'missing-neutral'){
                Write-WodenSessionJson (Join-Path $fake.trace 'stop-ack.json') @{schema=1;nonce=$fake.nonce;processId=42;startedUtcTicks=$identity.startedUtcTicks;revoked=$true;probeNeutral=$true}
            }
        }
        $select={if($fake.time -lt 32){[pscustomobject]@{state='waiting'}}else{$fixtureClient}}
        $census={
            $birth=[DateTime]::new($identity.startedUtcTicks,[DateTimeKind]::Utc)
            $rows=@()
            if($fake.mode -ceq 'replacement'){$rows=@([pscustomobject]@{processId=42;createdUtc=$birth.AddSeconds(1);precision='Tick100ns'})}
            if($fake.mode -ceq 'unknown-birth'){$rows=@([pscustomobject]@{processId=42;createdUtc=$null;precision='Tick100ns'})}
            $snap=[pscustomobject]@{querySucceeded=$true;rows=$rows}
            [pscustomobject]@{querySucceeded=$true;identities=@(Get-StageProcessIdentityDisposition -Recorded ([pscustomobject]@{processId=42;createdUtc=$birth;precision='Tick100ns'}) -Snapshot $snap)}
        }
        $r=Invoke-WodenGameSession -Identity $identity -RequestId $id -ProbeRoot $probe -ResultDirectory $case -LaunchRequest $request -SelectClient $select `
            -Gameplay {$fake.plays++;[pscustomobject]@{completed=$true}} `
            -StopCompanion {$fake.stopCalls++;$fake.companionStopped=$true;if($fake.mode -ceq 'stop-error'){throw 'simulated stop refusal'}} `
            -CompanionExited {$fake.companionStopped} -Census $census -Restore {$fake.restores++} `
            -Elapsed {$fake.time} -Wait $wait -Budgets @{RenderReadyTimeoutMs=40;ActivationTimeoutMs=10;InputReadyTimeoutMs=5;ArmTimeoutMs=5;StopTimeoutMs=10;QuiescenceTimeoutMs=15}
        Check ($fake.stopCalls -eq 1) "$mode stops companion once"
        Check (!(Test-Path -LiteralPath $request) -and (Test-Path -LiteralPath (Join-Path $probe 'revoke.json'))) "$mode removes one-shot and terminally revokes"
        $fr=Read-WodenSessionJson (Join-Path $case 'foreground-request.json')
        Check ($fr.window -eq 99 -and $fr.class -ceq 'UnityWndClass' -and $fr.startedUtcTicks -eq $identity.startedUtcTicks) "$mode request binds RenderReady identity"
        if($mode -in 'good','replacement'){
            Check ($r.status -ceq 'completed' -and $r.restored -and $fake.restores -eq 1 -and $fake.plays -eq 1) "$mode restores only after acknowledgments"
            Check ($r.states[1].state -ceq 'RenderReady' -and $r.states[1].elapsedMs -eq 32) 'Renderer delay does not consume activation budget'
        }elseif($mode -in 'missing-neutral','stop-error','unknown-birth'){
            Check ($r.status -ceq 'cleanup-pending' -and !$r.restored -and $fake.restores -eq 0) "$mode retains custody"
        }else{
            Check ($r.status -ceq 'failed' -and $r.restored -and $fake.plays -eq 0) "$mode sends zero gameplay and safely restores"
        }
        if($mode -ceq 'late-arm'){Check ($fake.armSeen -and $r.reason -ceq 'arm-unacknowledged') 'Late arm is bounded and revoked'}
    }
    # Neutral receipts cannot cross nonce or process birth; foreign one-shot survives.
    $ack=@{schema=1;nonce=$nonce;processId=42;startedUtcTicks=$identity.startedUtcTicks;revoked=$true;probeNeutral=$true}
    Check (!(Test-WodenProbeNeutral $ack ('c'*32) $identity)) 'Foreign nonce neutral acknowledgment refused'
    $ack.startedUtcTicks++;Check (!(Test-WodenProbeNeutral $ack $nonce $identity)) 'Replacement birth neutral acknowledgment refused'
    Write-WodenSessionJson $request @{Id=[guid]::NewGuid().ToString()}
    try{Remove-WodenLaunchRequest $request $id;throw 'Unexpected foreign request removal'}catch{if($_.Exception.Message -cne 'Foreign one-shot request; no removal'){throw}}
    Check (Test-Path -LiteralPath $request) 'Foreign one-shot remains intact'
    # Exercise the actual companion CLI binding/exit path against a fake script,
    # protecting only this child process's own standard handles. No game route.
    $companion=Join-Path $root 'fake-companion.ps1'
    [IO.File]::WriteAllText($companion,'param([string]$GameDir,[guid]$RequestId,[string]$CaseId,[int]$StartupTimeoutSeconds,[int]$SessionTimeoutMinutes,[switch]$ExternalLaunch,[string]$InputOrigin,[string]$NativeObservationSource,[int]$NativeObservationSeconds) [pscustomobject]@{game=$GameDir;id=$RequestId.ToString();caseId=$CaseId;startup=$StartupTimeoutSeconds;minutes=$SessionTimeoutMinutes;external=[bool]$ExternalLaunch;origin=$InputOrigin;reference=$NativeObservationSource;seconds=$NativeObservationSeconds}|ConvertTo-Json -Compress; exit 7')
    $argsForChild=New-WodenCompanionArguments -OwnedHelper (Join-Path $SharedRoot 'Stage-OwnedProcess.ps1') -OwnedHelperSha256 'C34C5891813736542E29C37F6A374DAAAE00A61DA7A8E83282E3F34D12D0F2E4' -CompanionScript $companion -GameDirectory 'C:/fixture/game with spaces' -RequestId $id -CaseId 'fixture-case' -StartupTimeoutSeconds 180 -NativeObservationSource 'C:/fixture/reference with spaces' -NativeObservationSeconds 10
    $answer=& pwsh @argsForChild
    Check ($LASTEXITCODE -eq 7) 'Companion bootstrap preserves nonzero script exit'
    $a=$answer|ConvertFrom-Json
    Check ($a.game -ceq 'C:/fixture/game with spaces' -and $a.id -ceq $id.ToString() -and $a.caseId -ceq 'fixture-case' -and $a.startup -eq 180 -and $a.minutes -eq 5 -and $a.external -and $a.origin -ceq 'automated' -and $a.reference -ceq 'C:/fixture/reference with spaces' -and $a.seconds -eq 10) 'Typed companion named arguments survive spaces and switches'
    Write-Output "PASS: $count NEW Woden consumer callback/stop/restore checks; simulated game/native operations, one finite fake companion CLI."
}finally{
    # Only this freshly created temporary fixture directory is removed.
    $resolved=[IO.Path]::GetFullPath($root)
    if(!$resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -notlike 'woden-session-*'){throw 'Fixture cleanup boundary refused'}
    Remove-Item -LiteralPath $resolved -Recurse
}
