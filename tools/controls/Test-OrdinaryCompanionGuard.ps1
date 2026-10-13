#Requires -Version 7.0
# Disposable delayed-companion fixture; no game, owner files, device or rig lease.
$ErrorActionPreference='Stop'
. "$PSScriptRoot/OrdinaryCompanionGuard.ps1"
$temp=Join-Path ([IO.Path]::GetTempPath()) ('woden-companion-guard-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
$script=Join-Path $temp 'delayed-companion.ps1'
$guard=Join-Path $temp 'ordinary-companion-guard.json'
$id=[guid]::NewGuid().ToString('N')
$exe=(Get-Command pwsh).Source
'Start-Sleep -Seconds 4' | Set-Content -LiteralPath $script
$checks=0
function Refuse {
    $refused=$false
    try {Assert-OrdinaryCompanionQuiescent -Path $guard -RequestId $id -ExpectedExecutable $exe -ExpectedScript $script}catch{$refused=$true}
    if(!$refused){throw 'Delayed or unobserved companion was allowed to restore owner state.'}
    $script:checks++
}
Assert-OrdinaryCompanionQuiescent -Path $guard -RequestId $id -ExpectedExecutable $exe -ExpectedScript $script;$checks++ # No launch attempted.
Write-OrdinaryCompanionGuard -Path $guard -State pending -RequestId $id -Executable $exe -Script $script
Refuse # Intent is durable before Process.Start.
$start=[Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-NoProfile','-File',$script)){[void]$start.ArgumentList.Add($arg)}
$child=[Diagnostics.Process]::Start($start)
try {
    if(!$child){throw 'Delayed fixture companion did not start.'}
    if(![string]::Equals($child.MainModule.FileName,$exe,[StringComparison]::OrdinalIgnoreCase)){throw 'Delayed fixture companion executable differs.'}
    $ticks=$child.StartTime.ToUniversalTime().Ticks
    Write-OrdinaryCompanionGuard -Path $guard -State running -RequestId $id -Executable $exe -Script $script -ProcessId $child.Id -StartedUtcTicks $ticks
    Refuse # No game has appeared; a companion is alive.
    Write-OrdinaryCompanionGuard -Path $guard -State exited -RequestId $id -Executable $exe -Script $script -ProcessId $child.Id -StartedUtcTicks $ticks -GameObserved $true -ExitCode 0
    Refuse # A premature exited marker cannot release a live companion.
    if(!$child.WaitForExit(7000)){throw 'Delayed fixture companion did not exit.'}
    Write-OrdinaryCompanionGuard -Path $guard -State exited -RequestId $id -Executable $exe -Script $script -ProcessId $child.Id -StartedUtcTicks $ticks -ExitCode $child.ExitCode
    Refuse # Even after exit, no game was observed; a queued Steam launch is unresolved.
    Write-OrdinaryCompanionGuard -Path $guard -State exited -RequestId $id -Executable $exe -Script $script -ProcessId $child.Id -StartedUtcTicks $ticks -GameObserved $true -ExitCode $child.ExitCode
    Assert-OrdinaryCompanionQuiescent -Path $guard -RequestId $id -ExpectedExecutable $exe -ExpectedScript $script;$checks++
    $wrongScript=Join-Path $temp 'other-companion.ps1'
    $refused=$false
    try {Assert-OrdinaryCompanionQuiescent -Path $guard -RequestId $id -ExpectedExecutable $exe -ExpectedScript $wrongScript}catch{$refused=$true}
    if(!$refused){throw 'Changed companion script path was admitted.'};$checks++
} finally {if($child -and !$child.HasExited){$child.Kill();$child.WaitForExit()};if($child){$child.Dispose()}}
Write-Output "$checks ordinary companion guard checks passed; delayed child launched no game. Fixture: $temp"
