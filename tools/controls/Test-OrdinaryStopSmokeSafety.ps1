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

    $config = Join-Path $sourceFixture 'temporary.cfg'
    $ownerText = "[ForceFeedback]`r`nEnabled = true`r`n[Telemetry]`r`nEnabled = true`r`n[Diagnostics]`r`nRecordSession = true`r`n"
    [IO.File]::WriteAllText($config,$ownerText)
    Set-OrdinaryTelemetryDeliveryOff $config
    if ([IO.File]::ReadAllText($config) -cne $ownerText.Replace("[Telemetry]`r`nEnabled = true", "[Telemetry]`r`nEnabled = false")) {
        throw 'Temporary mute changed another config key.'
    }
    Set-OrdinaryTelemetryDeliveryOff $config 'ForceFeedback' 'Enabled'
    Set-OrdinaryTelemetryDeliveryOff $config 'Diagnostics' 'RecordSession'
    $muted=[IO.File]::ReadAllText($config)
    if($muted -match '= true'){throw 'Explicit diagnostic mute left an output flag on'}
    [IO.File]::WriteAllText($config,$ownerText) # The supervisor restores the whole saved config tree.
    if ([IO.File]::ReadAllText($config) -cne $ownerText) { throw 'Owner config fixture did not restore exactly.' }
    [IO.File]::WriteAllText($config,"[Telemetry]`nEnabled = true`n[Telemetry]`nEnabled = true`n")
    Refuses { Set-OrdinaryTelemetryDeliveryOff $config } 'exactly one Telemetry section'

    $stuck = Fake-Process $false 0
    Refuses { Assert-OrdinaryGameExit $stuck } 'did not exit normally'
    $badExit = Fake-Process $true 9
    Refuses { Assert-OrdinaryGameExit $badExit } 'exited with 9'
    $normal = Fake-Process $true 0
    Assert-OrdinaryGameExit $normal
    if ($normal.CloseCalls -ne 1 -or $normal.WaitCalls -ne 1 -or $normal.RefreshCalls -ne 1) { throw 'Exact normal-exit path was not exercised.' }
    $finishing = Fake-Process $true 0
    $restorable = Wait-OrdinaryGameExitForRestore $finishing
    if (!$restorable.exited -or $restorable.exitCode -ne 0 -or $finishing.CloseCalls -ne 0 -or $finishing.WaitCalls -ne 1) { throw 'Error-path restore did not wait without another Close.' }
    $crashed = Fake-Process $true 9
    $restorable = Wait-OrdinaryGameExitForRestore $crashed
    if ($restorable.exitCode -ne 9 -or $crashed.CloseCalls -ne 0) { throw 'Crash result was lost or restoration blocked after exit.' }
    $stillRunning = Fake-Process $false 0
    Refuses { Wait-OrdinaryGameExitForRestore $stillRunning } 'still alive after bounded restore wait'
    if ($stillRunning.CloseCalls -ne 0) { throw 'Restore wait must not send Close or Kill.' }

    # The native observation log, as actual 08 met it: while a writer holds it (the live game), the read is a sharing
    # violation; once that handle is closed, the same bytes pass, and a refused interval is still refused.
    $log = Join-Path $sourceFixture 'LogOutput.log'
    [IO.File]::WriteAllText($log, "[Info] Native observation phase=ended; captured-current-reader; samples=241`r`n")
    $writer = [IO.File]::Open($log, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try { Refuses { Assert-OrdinaryNativeObservationLog $log } 'being used by another process' } finally { $writer.Dispose() }
    Assert-OrdinaryNativeObservationLog $log
    [IO.File]::WriteAllText($log, "[Info] Native observation phase=ended; refused: no reader`r`n")
    Refuses { Assert-OrdinaryNativeObservationLog $log } 'did not complete its first-reader interval'
    # The supervisor reads that log only after the exact game exit and Closed, and before the finally restore.
    $parseTokens = $null; $parseErrors = $null
    $supervisor = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Run-OrdinaryStopSmoke.ps1'), [ref]$parseTokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw 'Supervisor does not parse.' }
    $commands = @($supervisor.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true))
    $exitAt = @($commands | Where-Object { $_.GetCommandName() -ceq 'Assert-OrdinaryGameExit' } | ForEach-Object { $_.Extent.StartOffset })
    $logGateAt = @($commands | Where-Object { $_.GetCommandName() -ceq 'Assert-OrdinaryNativeObservationLog' } | ForEach-Object { $_.Extent.StartOffset })
    $closedAt = @($commands | Where-Object { $_.GetCommandName() -ceq 'Closed' -and $exitAt.Count -eq 1 -and $_.Extent.StartOffset -gt $exitAt[0] } |
        ForEach-Object { $_.Extent.StartOffset } | Sort-Object | Select-Object -First 1)
    $logReads = @($supervisor.FindAll({ param($node) $node -is [Management.Automation.Language.MemberExpressionAst] -and
        $node.Expression.Extent.Text -ceq '$files' -and $node.Member.Extent.Text -ceq 'log' }, $true) | ForEach-Object { $_.Extent.StartOffset })
    $restoreAt = @($supervisor.FindAll({ param($node) $node -is [Management.Automation.Language.TryStatementAst] -and $node.Finally }, $true) |
        Where-Object { $_.Finally.Extent.Text -match 'Restore-ControlFiles' } | ForEach-Object { $_.Finally.Extent.StartOffset })
    if ($exitAt.Count -ne 1 -or $logGateAt.Count -ne 1 -or $closedAt.Count -ne 1 -or $restoreAt.Count -ne 1 -or $logReads.Count -lt 1 -or
        @($logReads | Where-Object { $_ -le $closedAt[0] -or $_ -ge $restoreAt[0] }).Count -ne 0 -or $logGateAt[0] -le $closedAt[0]) {
        throw 'The supervisor reads the game log before the exact exit and Closed, or after the owner restore.'
    }
    'Ordinary Stop smoke safety gates: PASS'
} finally {
    if (Test-Path -LiteralPath $request) { Remove-Item -LiteralPath $request }
    if (Test-Path -LiteralPath $sourceFixture) {
        foreach ($file in @('force-config.json','capture-profile.json','temporary.cfg','LogOutput.log')) {
            $path = Join-Path $sourceFixture $file
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
        [IO.Directory]::Delete($sourceFixture,$false)
    }
}
