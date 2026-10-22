# Consumer callbacks only. Import hash-pinned Stage-GameSession before invoking.
function New-WodenCompanionArguments {
    param([string]$OwnedHelper,[string]$OwnedHelperSha256,[string]$CompanionScript,
        [string]$GameDirectory,[guid]$RequestId,[string]$CaseId,[int]$StartupTimeoutSeconds,
        [string]$NativeObservationSource,[int]$NativeObservationSeconds)
    # CLI positional values bind to these typed parameters. Do not splat a
    # string array of '-Name' tokens into a PowerShell script as named args.
    $bootstrap='param([string]$helper,[string]$sha,[string]$scriptPath,[string]$game,[guid]$id,[string]$caseId,[int]$startup,[string]$reference,[int]$seconds) $ErrorActionPreference="Stop"; if((Get-FileHash -LiteralPath $helper).Hash -cne $sha){throw "Owned helper drift"}; . $helper; Protect-StageStdHandles; & $scriptPath -GameDir $game -RequestId $id -CaseId $caseId -StartupTimeoutSeconds $startup -SessionTimeoutMinutes 5 -ExternalLaunch -InputOrigin automated -NativeObservationSource $reference -NativeObservationSeconds $seconds; exit $LASTEXITCODE'
    @('-NoProfile','-CommandWithArgs',$bootstrap,$OwnedHelper,$OwnedHelperSha256,$CompanionScript,
        $GameDirectory,$RequestId.ToString(),$CaseId,$StartupTimeoutSeconds.ToString(),$NativeObservationSource,$NativeObservationSeconds.ToString())
}
function Write-WodenSessionJson {
    param([string]$Path,$Value,[switch]$CreateNew)
    Assert-ControlPlain $Path
    $temp=$Path+'.tmp-'+[guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temp,($Value|ConvertTo-Json -Depth 12 -Compress),[Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temp,$Path,!$CreateNew)
}
function Read-WodenSessionJson {
    param([string]$Path)
    if(!(Test-Path -LiteralPath $Path -PathType Leaf)){return $null}
    Assert-ControlPlain $Path
    if((Get-Item -LiteralPath $Path).Length -gt 8192){throw 'Oversize Woden session receipt'}
    Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json -DateKind String
}
function Revoke-WodenSession {
    param([string]$ProbeRoot,[string]$Nonce)
    # Independent of command sequence and an outstanding arm file. The probe
    # observes this terminal marker before processing commands or another pulse.
    Write-WodenSessionJson (Join-Path $ProbeRoot 'revoke.json') @{schema=1;nonce=$Nonce}
}
function Test-WodenProbeNeutral {
    param($Ack,[string]$Nonce,$Identity)
    $null -ne $Ack -and $Ack.schema -eq 1 -and $Ack.nonce -ceq $Nonce -and
        $Ack.processId -eq $Identity.processId -and $Ack.startedUtcTicks -eq $Identity.startedUtcTicks -and
        $Ack.revoked -is [bool] -and $Ack.revoked -and $Ack.probeNeutral -is [bool] -and $Ack.probeNeutral
}
function Remove-WodenLaunchRequest {
    param([string]$Path,[guid]$RequestId)
    $request=Read-WodenSessionJson $Path
    if($null -eq $request){return}
    if($request.Id -ne $RequestId.ToString()){throw 'Foreign one-shot request; no removal'}
    Remove-Item -LiteralPath $Path
}
function Invoke-WodenGameSession {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Identity,[Parameter(Mandatory)][guid]$RequestId,
        [Parameter(Mandatory)][string]$ProbeRoot,[Parameter(Mandatory)][string]$ResultDirectory,
        [Parameter(Mandatory)][string]$LaunchRequest,
        [Parameter(Mandatory)][scriptblock]$SelectClient,[Parameter(Mandatory)][scriptblock]$Gameplay,
        [Parameter(Mandatory)][scriptblock]$StopCompanion,[Parameter(Mandatory)][scriptblock]$CompanionExited,
        [Parameter(Mandatory)][scriptblock]$Census,[Parameter(Mandatory)][scriptblock]$Restore,
        [Parameter(Mandatory)][scriptblock]$Elapsed,[Parameter(Mandatory)][scriptblock]$Wait,
        [hashtable]$Budgets=@{}
    )
    $nonce=$RequestId.ToString('N');$trace=Join-Path $ProbeRoot 'trace'
    $observe={param($client)
        $receipt=Read-WodenSessionJson (Join-Path $trace 'game-focus.json')
        Get-StageGameFocusWitness -Client $client -GameFocusWitness $receipt
    }
    $activate={param($client,$deadline)
        # Shared code calls once at RenderReady. Provider/operator owns the real
        # Sky activation. This file never substitutes a PowerShell focus call.
        $remaining=$deadline-(& $Elapsed)
        if($remaining -le 0){throw 'Expired foreground publication'}
        $request=New-StageForegroundRequest -Client $client -DeadlineUtc ([DateTimeOffset]::UtcNow.AddMilliseconds($remaining))
        Write-WodenSessionJson (Join-Path $ResultDirectory 'foreground-request.json') $request -CreateNew
        [pscustomobject]@{published=$true;activationBackend='Provider'}
    }
    $arm={param($client,$deadline)
        if(Test-Path -LiteralPath (Join-Path $ProbeRoot 'revoke.json')){throw 'Session already revoked'}
        if(!(Test-Path -LiteralPath $trace -PathType Container)){throw 'Cold probe did not admit'}
        $command=@{sequence=1;nonce=$nonce;operation='arm';client=@{
            processId=$Identity.processId;startedUtcTicks=$Identity.startedUtcTicks;executablePath=$Identity.executablePath;
            window=$client.window;class=$client.class}}
        Write-WodenSessionJson (Join-Path $trace 'command.json') $command -CreateNew
        while((& $Elapsed) -lt $deadline){
            if(Test-Path -LiteralPath (Join-Path $ProbeRoot 'revoke.json')){throw 'Arm revoked while pending'}
            $reply=Read-WodenSessionJson (Join-Path $trace 'reply.json')
            if($null -ne $reply){
                if($reply.nonce -cne $nonce -or $reply.sequence -ne 1 -or $reply.armSequence -ne 1 -or
                    $reply.ok -isnot [bool] -or !$reply.ok -or $reply.armed -isnot [bool] -or !$reply.armed -or $reply.reply -cne 'armed'){
                    throw 'Arm acknowledgment nonce/sequence/state mismatch'
                }
                return [pscustomobject]@{acknowledged=$true}
            }
            & $Wait
        }
        [pscustomobject]@{acknowledged=$false}
    }
    $stop={param($reason,$deadline)
        # Each action gets its own attempt even when an earlier action fails.
        $errors=[Collections.Generic.List[string]]::new()
        try{Revoke-WodenSession $ProbeRoot $nonce}catch{$errors.Add($_.Exception.Message)}
        try{
            if((Test-Path -LiteralPath $trace -PathType Container) -and !(Test-Path -LiteralPath (Join-Path $trace 'command.json'))){
                Write-WodenSessionJson (Join-Path $trace 'command.json') @{sequence=2;nonce=$nonce;operation='stop'} -CreateNew
            }
        }catch{$errors.Add($_.Exception.Message)}
        try{& $StopCompanion $reason $deadline}catch{$errors.Add($_.Exception.Message)}
        try{Remove-WodenLaunchRequest $LaunchRequest $RequestId}catch{$errors.Add($_.Exception.Message)}
        if($errors.Count){throw ($errors -join '; ')}
    }
    $stopped={
        $ack=Read-WodenSessionJson (Join-Path $trace 'stop-ack.json')
        [pscustomobject]@{probeNeutral=(Test-WodenProbeNeutral $ack $nonce $Identity);
            companionStopped=(& $CompanionExited);launchRequestAbsent=!(Test-Path -LiteralPath $LaunchRequest)}
    }
    $result=Invoke-StageGameSessionSequence -Identity $Identity -SelectClient $SelectClient -ObserveFocus $observe `
        -ActivationBackend Provider -Activate $activate -Arm $arm -Gameplay $Gameplay -Stop $stop `
        -ObserveStopped $stopped -Census $Census -Restore $Restore -Elapsed $Elapsed -Wait $Wait @Budgets
    Write-WodenSessionJson (Join-Path $ResultDirectory 'session-result.json') $result
    $result
}
