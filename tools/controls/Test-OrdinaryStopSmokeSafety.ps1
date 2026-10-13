# Offline refusal checks. No game, package, device, lease or owner files.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/OrdinaryStopSmokeSafety.ps1"

function Refuses([scriptblock]$Action,[string]$Expected) {
    try { & $Action; throw "Unexpected pass: $Expected" }
    catch { if ($_.Exception.Message -notlike "*$Expected*") { throw } }
}
function Fake-Process([bool]$WaitResult,[int]$ExitCode) {
    $fake = [pscustomobject]@{HasExited=$false;ExitCode=$ExitCode;WaitResult=$WaitResult;CloseCalls=0;WaitCalls=0}
    $fake | Add-Member ScriptMethod CloseMainWindow { $this.CloseCalls++; return $true }
    $fake | Add-Member ScriptMethod WaitForExit { param($milliseconds) $this.WaitCalls++; return $this.WaitResult }
    return $fake
}

$request = Join-Path ([IO.Path]::GetTempPath()) ('woden-ordinary-safety-' + [guid]::NewGuid().ToString('N'))
try {
    $running = Fake-Process $false 0
    Refuses { Assert-OrdinaryCompanionStopped ([pscustomobject]@{process=$running}) $request } 'Companion can still launch Woden'
    if ($running.WaitCalls -ne 1) { throw 'Companion exit gate was not invoked.' }
    $exited = Fake-Process $true 0
    [IO.File]::WriteAllText($request,'pending')
    Refuses { Assert-OrdinaryCompanionStopped ([pscustomobject]@{process=$exited}) $request } 'One-shot launch request remains'
    Remove-Item -LiteralPath $request
    Assert-OrdinaryCompanionStopped ([pscustomobject]@{process=$exited}) $request

    Refuses { Assert-OrdinaryNoDrivingRefusal 0 'REPROCESS REFUSED: Insufficient driving coverage: 0 samples' } 'unexpectedly produced'
    Refuses { Assert-OrdinaryNoDrivingRefusal 2 'REPROCESS REFUSED: broken finalizer' } 'expected no-driving refusal'
    Assert-OrdinaryNoDrivingRefusal 2 'REPROCESS REFUSED: Insufficient driving coverage: 0 samples over 0.000s'

    $stuck = Fake-Process $false 0
    Refuses { Assert-OrdinaryGameExit $stuck } 'did not exit normally'
    $badExit = Fake-Process $true 9
    Refuses { Assert-OrdinaryGameExit $badExit } 'exited with 9'
    $normal = Fake-Process $true 0
    Assert-OrdinaryGameExit $normal
    if ($normal.CloseCalls -ne 1 -or $normal.WaitCalls -ne 1) { throw 'Exact normal-exit path was not exercised.' }
    'Ordinary Stop smoke safety gates: PASS'
} finally {
    if (Test-Path -LiteralPath $request) { Remove-Item -LiteralPath $request }
}
