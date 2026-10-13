# Offline refusal checks. No game, package, device, lease or owner files.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/OrdinaryStopSmokeSafety.ps1"

function Refuses([scriptblock]$Action,[string]$Expected) {
    try { & $Action; throw "Unexpected pass: $Expected" }
    catch { if ($_.Exception.Message -notlike "*$Expected*") { throw } }
}
function Fake-Process([bool]$WaitResult,[int]$ExitCode) {
    $fake = [pscustomobject]@{HasExited=$false;ExitCode=$ExitCode;WaitResult=$WaitResult;CloseCalls=0;WaitCalls=0;RefreshCalls=0}
    $fake | Add-Member ScriptMethod CloseMainWindow { $this.CloseCalls++; return $true }
    $fake | Add-Member ScriptMethod WaitForExit { param($milliseconds) $this.WaitCalls++; return $this.WaitResult }
    $fake | Add-Member ScriptMethod Refresh { $this.RefreshCalls++ }
    return $fake
}

$request = Join-Path ([IO.Path]::GetTempPath()) ('woden-ordinary-safety-' + [guid]::NewGuid().ToString('N'))
$sourceFixture = Join-Path ([IO.Path]::GetTempPath()) ('woden-ordinary-source-' + [guid]::NewGuid().ToString('N'))
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

    [IO.Directory]::CreateDirectory($sourceFixture) | Out-Null
    $forceConfig = Join-Path $sourceFixture 'force-config.json'
    $profile = Join-Path $sourceFixture 'capture-profile.json'
    [IO.File]::WriteAllText($forceConfig,'{}')
    [IO.File]::WriteAllText($profile,'{"schema":"woden.capture-profile","ffbEnabled":true}')
    $id = [guid]::NewGuid()
    $header = [pscustomobject]@{kind='metadata';metadata=[pscustomobject]@{properties=[pscustomobject]@{
        requestId=$id.ToString();disableForces='true';forceConfigSha256=(Get-FileHash $forceConfig).Hash
        captureProfileSha256=(Get-FileHash $profile).Hash
    }}}
    Assert-OrdinarySourceMute $header $id $forceConfig $profile
    $header.metadata.properties.disableForces='false'
    Refuses { Assert-OrdinarySourceMute $header $id $forceConfig $profile } 'does not prove this no-force request'

    $stuck = Fake-Process $false 0
    Refuses { Assert-OrdinaryGameExit $stuck } 'did not exit normally'
    $badExit = Fake-Process $true 9
    Refuses { Assert-OrdinaryGameExit $badExit } 'exited with 9'
    $normal = Fake-Process $true 0
    Assert-OrdinaryGameExit $normal
    if ($normal.CloseCalls -ne 1 -or $normal.WaitCalls -ne 1 -or $normal.RefreshCalls -ne 1) { throw 'Exact normal-exit path was not exercised.' }
    'Ordinary Stop smoke safety gates: PASS'
} finally {
    if (Test-Path -LiteralPath $request) { Remove-Item -LiteralPath $request }
    if (Test-Path -LiteralPath $sourceFixture) {
        foreach ($file in @('force-config.json','capture-profile.json')) {
            $path = Join-Path $sourceFixture $file
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
        [IO.Directory]::Delete($sourceFixture,$false)
    }
}
