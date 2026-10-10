#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Result,[Parameter(Mandatory)][ValidateSet('status','raw','stop')][string]$Operation,[string]$Raw)
$ErrorActionPreference='Stop'
$Result=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Result)
. "$PSScriptRoot/OwnerFiles.ps1"
. "$PSScriptRoot/../../vendor/playback/Stage-RigLease.ps1"
Assert-ControlPlain $Result
$c=Get-Content -LiteralPath "$Result/context.json" -Raw | ConvertFrom-Json
if($c.schema -ne 1 -or $c.nonce -notmatch '^[a-f0-9]{32}$' -or $c.nonce -eq ('0'*32)){throw 'Invalid command context.'}
if($Operation -eq 'raw') {
    if(!$Raw -or !$Raw.StartsWith('inject raw ') -or $Raw.Length -gt 512 -or $Raw -match '[^\x20-\x7e]'){throw 'Only bounded raw commands are supported.'}
} elseif($Raw){throw 'Unexpected raw command.'}
$slot=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
function Assert-Identity {
    if($c.lease.path -ne $slot){throw 'Lease scope differs.'}
    Assert-StageRigLease -Path $slot -Token $c.lease.token
    $p=Get-Process -Id $c.process -ErrorAction Stop
    try {
        if($p.HasExited -or $p.StartTime.ToUniversalTime().Ticks -ne ([DateTimeOffset]$c.startedUtc).UtcTicks -or
           ![string]::Equals($p.MainModule.FileName,(Join-Path $c.gameDir 'Super Woden Rally Edge.exe'),[StringComparison]::OrdinalIgnoreCase)){throw 'Game identity changed.'}
    } finally {$p.Dispose()}
}
Assert-Identity
$lock=$null
try {
    $lock=[IO.File]::Open("$Result/command.lock",[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if(Test-Path "$Result/pending-command.json"){throw 'A prior command has unknown completion; do not resend.'}
    $last=if(Test-Path "$Result/last-sequence.txt"){[int](Get-Content "$Result/last-sequence.txt" -Raw)}else{0}
    $next=$last+1
    if($next -gt 1024){throw 'Command limit.'}
    $message=[ordered]@{sequence=$next;nonce=$c.nonce;operation=$Operation;raw=$(if($Operation -eq 'raw'){$Raw}else{$null})}
    $json=$message | ConvertTo-Json
    [IO.File]::WriteAllText("$Result/pending-command.json",$json)
    $temp="$Result/trace/command.next.json"; $target="$Result/trace/command.json"
    if(Test-Path $target){throw 'Probe command mailbox occupied.'}
    [IO.File]::WriteAllText($temp,$json)
    Assert-Identity
    [IO.File]::Move($temp,$target)
    $until=[DateTime]::UtcNow.AddSeconds(5); $reply=$null
    while([DateTime]::UtcNow -lt $until) {
        Assert-Identity
        if(Test-Path "$Result/trace/reply.json") {
            try {$r=Get-Content "$Result/trace/reply.json" -Raw | ConvertFrom-Json; if($r.nonce -ceq $c.nonce -and $r.sequence -eq $next){$reply=$r;break}}catch [ArgumentException]{}
        }
        Start-Sleep -Milliseconds 100
    }
    if(!$reply){throw 'No correlated reply; command completion unknown. No retry.'}
    [IO.File]::Move("$Result/pending-command.json","$Result/sent-$next.json")
    $reply | ConvertTo-Json | Set-Content "$Result/reply-$next.json"
    [IO.File]::WriteAllText("$Result/last-sequence.txt",[string]$next)
    if(!$reply.ok){throw $reply.reply}
    $reply.reply
} finally {if($lock){$lock.Dispose()}}
