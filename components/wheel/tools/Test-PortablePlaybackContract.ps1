#Requires -Version 7.0
# Source-only revision-5 ownership and pinned failure-result checks. No game, device or install.
$ErrorActionPreference = 'Stop'
$installer = Join-Path $PSScriptRoot 'Manage-Install.ps1'
$runner = Join-Path $PSScriptRoot '../../../tools/game/Run-StageSession.ps1'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($installer,[ref]$tokens,[ref]$errors)
if (@($errors).Count) { throw 'Installer did not parse.' }
foreach ($name in 'Expected-OrdinarySession','Assert-OrdinarySession','Assert-PlaybackCapabilities','Assert-ReceiptFiles') {
    $definition = @($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$false))
    if ($definition.Count -ne 1) { throw "Expected one installer function: $name" }
    Invoke-Expression $definition[0].Extent.Text
}
function Refuses([scriptblock]$action) {
    try { & $action; throw 'Expected refusal did not occur.' }
    catch { if ($_.Exception.Message -eq 'Expected refusal did not occur.') { throw } }
}
$gameHash = 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C'
$legacyPayloadNames = @('WodenRallyEdgeWheel.dll','WodenRallyEdge.Core.dll','Dbce.Wheel.Telemetry.dll','Dbce.Wheel.Recording.dll','Dbce.Wheel.Ffb.dll','WheelFfb.dll','recording-provenance.json','toolkit.version','telemetry-schema.json','Dbce.Wheel.Playback.dll','playback-provenance.json','playback-LICENSE.txt','Stage-Session.ps1')
$revision4PayloadNames = @($legacyPayloadNames + @('Start-RecordedGame.ps1','RecordedStopControl.ps1','WodenCaseFinalizer.exe'))
$playbackPayloadNames = @('WodenFullSession.ps1','ReplayEnvironment.ps1','Stage-RigLease.ps1','rig-lease-provenance.json','Stage-RunnerLifecycle.ps1','session-capabilities.json')
$payloadNames = @($revision4PayloadNames + $playbackPayloadNames)
$prefix = 'BepInEx\plugins\WodenRallyEdgeWheel\'
$legacyBasePaths = @($legacyPayloadNames | ForEach-Object { $prefix + $_ })
$revision4BasePaths = @($revision4PayloadNames | ForEach-Object { $prefix + $_ })
$basePaths = @($payloadNames | ForEach-Object { $prefix + $_ })
$shimPath = $prefix + 'dbce_shim.dll'
$entries4 = @($revision4BasePaths | ForEach-Object { [pscustomobject]@{path=$_;sha256=('a'*64)} })
$entries5 = @($basePaths | ForEach-Object { [pscustomobject]@{path=$_;sha256=('a'*64)} })
$ordinary4 = Expected-OrdinarySession $entries4 $true | ConvertTo-Json -Depth 8 | ConvertFrom-Json
$ordinary5 = Expected-OrdinarySession $entries5 $true | ConvertTo-Json -Depth 8 | ConvertFrom-Json
$old = [pscustomobject]@{installerRevision=4;mode='Install';files=$entries4;ordinarySession=$ordinary4}
$new = [pscustomobject]@{installerRevision=5;mode='Install';files=$entries5;ordinarySession=$ordinary5}
$null = Assert-ReceiptFiles $old
$null = Assert-ReceiptFiles $new
Refuses { $null = Assert-ReceiptFiles ([pscustomobject]@{installerRevision=4;mode='Install';files=$entries5;ordinarySession=$ordinary5}) }
Refuses { $null = Assert-ReceiptFiles ([pscustomobject]@{installerRevision=5;mode='Install';files=$entries4;ordinarySession=$ordinary4}) }
$capability = [pscustomobject]@{
    schema='dbce.session-capabilities@1';game='Super Woden Rally Edge';protocol='woden.full-session-package@1'
    runner='WodenFullSession.ps1';gameBuildSha256=$gameHash.ToLowerInvariant()
    record=[pscustomobject]@{enabled=$false}
    playback=[pscustomobject]@{enabled=$true;adapters=@('woden.native-arcade-v1@1')}
}
Assert-PlaybackCapabilities $capability
$capability.playback.enabled = $false
Refuses { Assert-PlaybackCapabilities $capability }
$capability.playback.enabled = $true
$capability.record.enabled = $true
Refuses { Assert-PlaybackCapabilities $capability }
$capability.record.enabled = $false
$lifecycle = Join-Path $PSScriptRoot '../../../vendor/playback/Stage-RunnerLifecycle.ps1'
if ((Get-FileHash -LiteralPath $lifecycle -Algorithm SHA256).Hash -ne '4BA9A38519C981E4B9D7CF3C9765C9DCB0ECB4C15F3720CDB197C6DF4A27F62C') { throw 'Vendored lifecycle differs from reviewed source.' }
$fixtureRoot = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/portable-playback-contract-' + [guid]::NewGuid().ToString('N'))
$requestId = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
& pwsh -NoProfile -File $runner -AdmittedPackage (Join-Path $fixtureRoot 'missing-package') -Result $fixtureRoot -GameDir (Join-Path $fixtureRoot 'missing-game') -RequestId $requestId -PackageSealSha256 ('b'*64) 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) { throw 'Missing admitted package unexpectedly launched.' }
$terminal = Get-Content -LiteralPath (Join-Path $fixtureRoot 'result.json') -Raw | ConvertFrom-Json
if ($terminal.schema -ne 'woden.full-session-result@1' -or $terminal.requestId -ne $requestId -or $terminal.action -ne 'playback' -or
    $terminal.passed -or $terminal.ownerRestored -or $terminal.closed -or $terminal.outputsMuted -or
    $null -ne $terminal.gameExitCode -or $null -ne $terminal.sessionSha256 -or $null -ne $terminal.stageSealSha256 -or
    $terminal.message.Length -gt 512) { throw 'Failed pinned request made an unsupported terminal claim.' }
# Pure terminal-result exercise: an observed nonzero game exit never passes even
# when the replay status, mute policy and owner restoration otherwise succeeded.
. $lifecycle
$runnerTokens = $null; $runnerErrors = $null
$runnerAst = [Management.Automation.Language.Parser]::ParseFile($runner,[ref]$runnerTokens,[ref]$runnerErrors)
if (@($runnerErrors).Count) { throw 'Runner did not parse.' }
$writer = @($runnerAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Write-TerminalResult'},$false))
if ($writer.Count -ne 1) { throw 'Expected one terminal result writer.' }
Invoke-Expression $writer[0].Extent.Text
$exitReader = @($runnerAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-ObservedGameExitEvidence'},$false))
if ($exitReader.Count -ne 1) { throw 'Expected one runner game-exit reader.' }
Invoke-Expression $exitReader[0].Extent.Text
$unknownExit = Get-ObservedGameExitEvidence -Process ([pscustomobject]@{HasExited=$true;ExitCode=$null;Id=1234}) -ObservedHandle ([IntPtr]::new(1))
if (!$unknownExit.exited -or $null -ne $unknownExit.exitCode -or $unknownExit.shutdownPass -or !$unknownExit.handleObservedWhileAlive) {
    throw 'Runner converted an unavailable exit code into a clean exit.'
}
$cleanExit = Get-ObservedGameExitEvidence -Process ([pscustomobject]@{HasExited=$true;ExitCode=[int]0;Id=1234}) -ObservedHandle ([IntPtr]::new(1))
if (!$cleanExit.shutdownPass -or $cleanExit.exitCode -ne 0) { throw 'Runner missed an observed exit code 0.' }
Refuses { Get-ObservedGameExitEvidence -Process ([pscustomobject]@{HasExited=$true;ExitCode=[int]0;Id=1234}) -ObservedHandle ([IntPtr]::Zero) }
$RequestId = $requestId; $resultMayWrite = $true; $AdmittedPackage = Join-Path $fixtureRoot 'derived'
$admittedSessionSha = 'c'*64; $verifiedPackageSeal = 'b'*64; $admittedSeal = 'd'*64
$sessionSucceeded = $true; $outputsMuted = $true; $runFailure = $null
foreach ($code in 0,1) {
    $Result = Join-Path $fixtureRoot "exit-$code"
    $null = New-Item -ItemType Directory -Path $Result
    'exact owner restore fixture' | Set-Content -LiteralPath (Join-Path $Result 'restored.txt')
    $exitEvidence = New-StageGameExitEvidence -CloseMainWindowRequested:$false -Exited:$true -ExitCode ([int]$code) -ProcessId 1234
    Write-TerminalResult
    $actual = Get-Content -LiteralPath (Join-Path $Result 'result.json') -Raw | ConvertFrom-Json
    if ($actual.passed -ne ($code -eq 0) -or $actual.gameExitCode -ne $code -or !$actual.ownerRestored -or !$actual.closed -or !$actual.outputsMuted -or
        $actual.sessionSha256 -ne $admittedSessionSha -or $actual.packageSealSha256 -ne $verifiedPackageSeal -or $actual.stageSealSha256 -ne $admittedSeal) {
        throw "Terminal result misreported observed game exit $code."
    }
    Refuses { Write-TerminalResult } # CreateNew keeps the first terminal result immutable.
}
Write-Host "PASS: revision-4/5 ownership, qualified playback capability, lifecycle pin, bounded failure result and retained-handle numeric-exit binding. Fixture: $fixtureRoot"
