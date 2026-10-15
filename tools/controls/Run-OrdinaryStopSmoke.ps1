#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$PackageRoot,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$PackageManifestSha256,
    [Parameter(Mandatory)][string]$NativeCandidatePath,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$NativeSha256,
    [Parameter(Mandatory)][string]$NativeProvenancePath,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$NativeProvenanceSha256,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$OriginalReceiptSha256,
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$RigLeaseToken,
    [guid]$RequestId = [guid]::NewGuid(),
    [ValidateRange(1,10)][int]$MenuDwellSeconds = 2,
    [string]$NativeObservationSource,
    [ValidateRange(5,15)][int]$NativeObservationSeconds = 10,
    [ValidateRange(30,300)][int]$StartupTimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/OwnerFiles.ps1"
. "$PSScriptRoot/Environment.ps1"
. "$PSScriptRoot/OrdinaryStopSmokeSafety.ps1"
. 'E:/Source/toolkits/dbce-wheel-mod-toolkit/tools/playback/Stage-RigLease.ps1'

function Pin([string]$Path,[string]$Hash) {
    Assert-ControlPlain $Path
    if ((Get-ControlHash $Path) -ne $Hash) { throw "Pinned input differs: $Path" }
}
function Closed {
    if (Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue) { throw 'Woden remains open; owner-state changes refused.' }
}
function Status([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $fields = @{}
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        $at = $line.IndexOf('=')
        if ($at -le 0 -or $fields.ContainsKey($line.Substring(0,$at))) { throw 'Malformed recording status.' }
        $fields[$line.Substring(0,$at)] = $line.Substring($at+1)
    }
    return $fields
}
function Owned([object]$Receipt,[string]$Relative,[string]$Hash) {
    $owned = @($Receipt.files | Where-Object { $_.path.Replace('\','/') -ceq $Relative })
    if ($owned.Count -ne 1 -or $owned[0].sha256 -ne $Hash -or (Get-ControlHash (Join-Path $GameDir $Relative)) -ne $Hash) {
        throw "Installed receipt does not own exact payload: $Relative"
    }
}
function Assert-Inventory([object]$Before,$Roots,$Files) {
    $now = @(Get-ControlInventory $Roots $Files | ForEach-Object { [pscustomobject]@{key=$_.key;hash=(Get-ControlHash $_.path)} } | Sort-Object key)
    $expected = @($Before.entries | Sort-Object key)
    if ($now.Count -ne $expected.Count) { throw 'Owner inventory count differs after restoration.' }
    for ($i=0;$i -lt $now.Count;$i++) {
        if ($now[$i].key -cne $expected[$i].key -or $now[$i].hash -cne $expected[$i].hash) { throw "Owner inventory differs: $($expected[$i].key)" }
    }
}
function Start-Companion([string]$Script,[string]$OutputRoot,[guid]$Id) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh).Source)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $casePrefix = if ($NativeObservationSource) { 'woden-native-observation-' } else { 'woden-menu-smoke-' }
    foreach ($arg in @('-NoProfile','-File',$Script,'-GameDir',$GameDir,'-RequestId',$Id.ToString(),'-CaseId',($casePrefix + $Id.ToString('N')),'-StartupTimeoutSeconds',$StartupTimeoutSeconds.ToString(),'-SessionTimeoutMinutes','5')) { [void]$start.ArgumentList.Add($arg) }
    if ($NativeObservationSource) {
        foreach ($arg in @('-InputOrigin','automated','-NativeObservationSource',$NativeObservationSource,'-NativeObservationSeconds',$NativeObservationSeconds.ToString())) { [void]$start.ArgumentList.Add($arg) }
    }
    $child = [Diagnostics.Process]::Start($start)
    if ($null -eq $child) { throw 'Installed companion did not start.' }
    $stdout = $child.StandardOutput.ReadToEndAsync()
    $stderr = $child.StandardError.ReadToEndAsync()
    return [pscustomobject]@{process=$child;stdout=$stdout;stderr=$stderr;outputRoot=$OutputRoot}
}
function Save-Companion($Child) {
    if (!$Child.process.WaitForExit(15000)) { throw 'Companion did not finish after source completion; retain lease.' }
    $Child.stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $Child.outputRoot 'companion.stdout.txt')
    $Child.stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $Child.outputRoot 'companion.stderr.txt')
    $Child.process.ExitCode | Set-Content -LiteralPath (Join-Path $Child.outputRoot 'companion.exit.txt')
    return $Child.process.ExitCode
}

$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$NativeCandidatePath = [IO.Path]::GetFullPath($NativeCandidatePath)
$NativeProvenancePath = [IO.Path]::GetFullPath($NativeProvenancePath)
$GameDir = [IO.Path]::GetFullPath($GameDir).TrimEnd('\','/')
$Result = [IO.Path]::GetFullPath($Result)
$slot = Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
$prefsKey = 'HKCU:\Software\ViJuDa\Super Woden Rally Edge'
$receiptPath = Join-Path $GameDir 'BepInEx/WodenWheel-install.json'
$plugin = Join-Path $GameDir 'BepInEx/plugins/WodenRallyEdgeWheel'
$relativeNative = 'BepInEx/plugins/WodenRallyEdgeWheel/WheelFfb.dll'
$manifestPath = Join-Path $PackageRoot 'manifest.json'
$metadataPath = Join-Path $PackageRoot 'automation-native-provenance.json'
$idText = $RequestId.ToString('N')
$recording = Join-Path $GameDir "BepInEx/WodenRecordings/request-$idText"
$requestPath = Join-Path $GameDir 'BepInEx/config/woden-record-next-launch.json'
$configPath = Join-Path $GameDir 'BepInEx/config/dbce.wodenrallyedgewheel.cfg'
$companionPath = Join-Path $plugin 'Start-RecordedGame.ps1'
$exe = Join-Path $GameDir 'Super Woden Rally Edge.exe'
$roots = [ordered]@{
    plugin=$plugin
    config=(Join-Path $GameDir 'BepInEx/config')
    save=(Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge')
    recording=$recording
}
$files = [ordered]@{
    receipt=$receiptPath
    log=(Join-Path $GameDir 'BepInEx/LogOutput.log')
}

# These checks precede the first filesystem or game mutation. The 24A9 native
# and current unarmed automation package intentionally fail this gate.
if ($RequestId -eq [guid]::Empty -or $NativeSha256 -ne '088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567' -or
    $NativeProvenanceSha256 -ne '6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5') {
    throw 'Exact qualified x64 load-muted native and provenance are required.'
}
foreach ($path in @($PackageRoot,$NativeCandidatePath,$NativeProvenancePath,$GameDir,$Result,$recording)) { Assert-ControlPlain $path }
if (Test-Path -LiteralPath $Result) { throw 'Result must be a new directory.' }
if (Test-Path -LiteralPath $recording) { throw 'RequestId recording directory already exists.' }
if (Test-Path -LiteralPath $requestPath) { throw 'A one-shot ordinary recording request already exists.' }
if ($NativeObservationSource) {
    $NativeObservationSource = [IO.Path]::GetFullPath($NativeObservationSource)
    Assert-ControlPlain $NativeObservationSource
    if (!(Test-Path -LiteralPath (Join-Path $NativeObservationSource 'complete.tsv') -PathType Leaf)) { throw 'Sealed native observation reference required.' }
}
Closed
Pin $receiptPath $OriginalReceiptSha256
Pin $manifestPath $PackageManifestSha256
Pin $NativeCandidatePath $NativeSha256
Pin $NativeProvenancePath $NativeProvenanceSha256
if ((Get-ControlHash (Join-Path $GameDir 'GameAssembly.dll')) -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C') { throw 'Unsupported game build.' }
$provenance = Get-Content -LiteralPath $NativeProvenancePath -Raw | ConvertFrom-Json
$nativeMarker = 'DBCE-WHEELFFB-NO-FORCE-TEST-BUILD'
if ($provenance.dllSha256 -ne $NativeSha256 -or $provenance.define -cne 'WHEELFFB_NO_FORCE_BUILD' -or
    $provenance.arch -cne 'x64' -or $provenance.marker -cne $nativeMarker -or
    $provenance.firstCallStatus -notmatch 'armed: no force \(no-force build\)' -or
    $provenance.flavor -notmatch '^no-force test build' -or $provenance.commit -cne 'eb77b909416b8a6ef70787e4621aaa62f16b9f45' -or
    $provenance.sourcesDirty -ne $false) { throw 'Native provenance does not identify the qualified x64 load-muted build.' }
if (![Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($NativeCandidatePath)).Contains($nativeMarker)) {
    throw 'Pinned native DLL lacks the no-force build marker.'
}
$packageMetadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($packageMetadata.schema -cne 'woden.automation-muted-native@1' -or $packageMetadata.nativeSha256 -ne $NativeSha256 -or
    $packageMetadata.nativeDefaultState -cne 'armed' -or $packageMetadata.requiresExplicitArm -ne $false -or
    $packageMetadata.toolkitNativeCommit -cne $provenance.commit -or $packageMetadata.toolkitNativeProvenanceSha256 -ne $NativeProvenanceSha256) {
    throw 'Package does not attest an armed-at-load internal native.'
}
$manifest = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
if ($manifest.Count -eq 0 -or @($manifest.path | Sort-Object -Unique).Count -ne $manifest.Count) { throw 'Package manifest is empty or duplicated.' }
foreach ($entry in $manifest) {
    if ($entry.path -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($entry.path) -or $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Unsafe package manifest entry.' }
    Pin (Join-Path $PackageRoot $entry.path) $entry.sha256
}
$nativeEntry = @($manifest | Where-Object { $_.path.Replace('\','/') -ceq $relativeNative })
if ($nativeEntry.Count -ne 1 -or $nativeEntry[0].sha256 -ne $NativeSha256) { throw 'Package manifest native differs from qualified candidate.' }
$packageSidecar = Join-Path $PackageRoot 'WheelFfb.noforce.provenance.json'
Pin $packageSidecar $NativeProvenanceSha256
Assert-StageRigLease -Path $slot -Token $RigLeaseToken

$mutated = $false
$restored = $false
$launched = $null
$child = $null
$errorText = $null
try {
    [IO.Directory]::CreateDirectory($Result) | Out-Null
    Save-ControlFiles (Join-Path $Result 'owner-before') $roots $files
    Registry-Snapshot (Join-Path $Result 'preferences.json')
    $before = Get-Content -LiteralPath (Join-Path $Result 'owner-before/manifest.json') -Raw | ConvertFrom-Json
    $prefsHash = Get-ControlHash (Join-Path $Result 'preferences.json')
    [ordered]@{schema=1;gameDir=$GameDir;requestId=$RequestId;packageManifestSha256=$PackageManifestSha256;nativeSha256=$NativeSha256;nativeProvenanceSha256=$NativeProvenanceSha256;originalReceiptSha256=$OriginalReceiptSha256;leasePath=$slot;ownerBackupSha256=(Get-ControlHash (Join-Path $Result 'owner-before/manifest.json'));preferencesSha256=$prefsHash} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Result 'recovery.json')
    $mutated = $true
    & pwsh -NoProfile -File (Join-Path $PackageRoot 'Manage-Install.ps1') -Mode Install -PackageRoot $PackageRoot -GameDir $GameDir *> (Join-Path $Result 'install.log')
    if ($LASTEXITCODE -ne 0) { throw "Candidate installer failed: $LASTEXITCODE" }
    $installed = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $expectedPayloads = @($manifest | Where-Object { $_.path.Replace('\','/').StartsWith('BepInEx/plugins/WodenRallyEdgeWheel/',[StringComparison]::OrdinalIgnoreCase) })
    $expectedPaths = @($expectedPayloads | ForEach-Object { $_.path.Replace('\','/').ToLowerInvariant() } | Sort-Object)
    $installedPaths = @($installed.files | ForEach-Object { $_.path.Replace('\','/').ToLowerInvariant() } | Sort-Object)
    if ($installed.mode -cne 'Install' -or $installed.installerRevision -notin @(4,5) -or @($installed.files).Count -ne $expectedPayloads.Count -or
        @(Compare-Object $expectedPaths $installedPaths).Count -ne 0 -or
        $installed.ordinarySession.schema -cne 'woden.signal-session@1' -or $installed.ordinarySession.record -ne $true -or
        $installed.ordinarySession.playback -ne $false) { throw 'Installed receipt lacks the reviewed private ordinary capability.' }
    foreach ($entry in $installed.files) { Pin (Join-Path $GameDir $entry.path) $entry.sha256 }
    Owned $installed $relativeNative $NativeSha256
    foreach ($name in @('Start-RecordedGame.ps1','RecordedStopControl.ps1','WodenCaseFinalizer.exe','Dbce.Wheel.Recording.dll')) {
        $relative = "BepInEx/plugins/WodenRallyEdgeWheel/$name"
        $entry = @($manifest | Where-Object { $_.path.Replace('\','/') -ceq $relative })
        if ($entry.Count -ne 1) { throw "Companion package payload missing: $name" }
        Owned $installed $relative $entry[0].sha256
    }
    $capabilityFields = [ordered]@{
        pluginSha256='WodenRallyEdgeWheel.dll';coreSha256='WodenRallyEdge.Core.dll';recordingSha256='Dbce.Wheel.Recording.dll'
        sourceToolSha256='Start-RecordedGame.ps1';stopToolSha256='RecordedStopControl.ps1';finalizerSha256='WodenCaseFinalizer.exe'
    }
    foreach ($field in $capabilityFields.Keys) {
        $relative = 'BepInEx/plugins/WodenRallyEdgeWheel/' + $capabilityFields[$field]
        $entry = @($manifest | Where-Object { $_.path.Replace('\','/') -ceq $relative })
        if ($entry.Count -ne 1 -or $installed.ordinarySession.$field -ne $entry[0].sha256) { throw "Installed ordinary capability differs: $field" }
    }
    if ($installed.sharedInputCandidate.controls.nativeSha256 -ne $NativeSha256) { throw 'Installed shared candidate native pin differs.' }
    # The temporary config disables loopback telemetry delivery only. Publish,
    # force analysis and the request-bound source recorder remain active.
    Set-OrdinaryTelemetryDeliveryOff $configPath
    [ordered]@{schema=1;config='Telemetry/Enabled = false';temporaryConfigSha256=(Get-ControlHash $configPath)} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'telemetry-delivery-mute.json')
    Assert-StageRigLease -Path $slot -Token $RigLeaseToken
    Closed
    $child = Start-Companion $companionPath $Result $RequestId
    $statusPath = Join-Path $recording 'control/status.txt'
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        $status = Status $statusPath
        if ($status -and $status.id -ceq $idText -and $status.status -ceq 'Recording' -and $status.active -ceq 'True') { break }
        if ($child.process.HasExited) { throw 'Installed companion exited before Recording status.' }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if (!$status -or $status.status -cne 'Recording') { throw 'Timed out waiting for exact Recording status.' }
    $pidValue = 0; $ticks = [long]0
    if (![int]::TryParse($status.pid,[ref]$pidValue) -or ![long]::TryParse($status.startedUtcTicks,[ref]$ticks)) { throw 'Recorder process identity is invalid.' }
    $launched = Get-Process -Id $pidValue -ErrorAction Stop
    if ($launched.StartTime.ToUniversalTime().Ticks -ne $ticks -or ![string]::Equals($launched.Path,$exe,[StringComparison]::OrdinalIgnoreCase) -or
        @(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue).Count -ne 1) { throw 'Exact Woden process identity differs.' }
    if (Test-Path -LiteralPath $requestPath) {
        $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
        if ($request.Id -ne $RequestId.ToString() -or $request.DisableForces -ne $true) { throw 'One-shot request did not demand force disablement.' }
    }
    if (!$NativeObservationSource) {
        Start-Sleep -Seconds $MenuDwellSeconds
        & pwsh -NoProfile -File $companionPath -GameDir $GameDir -Stop -RequestId $RequestId *> (Join-Path $Result 'stop.log')
        if ($LASTEXITCODE -ne 0) { throw "Installed request-bound Stop refused: $LASTEXITCODE" }
    }
    # Native observation owns its bounded first-reader interval and Stop/normal exit; never inject raw driving here.
    $deadline = [DateTime]::UtcNow.AddSeconds($(if ($NativeObservationSource) { 210 } else { 20 }))
    do {
        $status = Status $statusPath
        if ($status -and $status.id -ceq $idText -and $status.status -ceq 'Completed' -and $status.active -ceq 'False') { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if (!$status -or $status.status -cne 'Completed') { throw 'Request-bound Stop did not complete original source.' }
    $source = Join-Path $recording 'source.jsonl'
    $header = Get-Content -LiteralPath $source -TotalCount 1 | ConvertFrom-Json
    $footer = Get-Content -LiteralPath $source -Tail 1 | ConvertFrom-Json
    Assert-OrdinarySourceMute $header $RequestId (Join-Path $recording 'force-config.json') (Join-Path $recording 'capture-profile.json')
    if ($footer.kind -cne 'footer' -or $footer.footer.completed -ne $true -or $footer.footer.stopReason -cne 'stopped') { throw 'Original source footer is not a completed Stop.' }
    foreach ($count in @('contentionCount','droppedMarkers','droppedSamples','errorCount','invalidCount','limitCount','queueFullCount','rejectedAfterStop')) {
        if ($footer.footer.counts.$count -ne 0) { throw "Original source footer reports $count." }
    }
    if ($footer.footer.counts.acceptedSamples -le 0) { throw 'Original diagnostic source has no samples.' }
    $driving = 0; $nativeSamples = 0
    foreach ($line in [IO.File]::ReadLines($source)) {
        if ($line -notmatch '"kind":"sample"') { continue }
        $sample = $line | ConvertFrom-Json
        if ($sample.sample.channels.'sample.driving' -eq 1) { $driving++ }
        $names = @($sample.sample.channels.PSObject.Properties.Name)
        if (@('wheelRaw.throttle','wheelRaw.brake','wheelRaw.handbrake','wheelInput.throttle','wheelInput.brake','wheelInput.handbrake','controls.throttle','controls.brake','game.throttle','game.brake','game.handbrake' | Where-Object { $_ -notin $names }).Count -eq 0) { $nativeSamples++ }
    }
    if (!$NativeObservationSource -and $driving -ne 0) { throw 'Menu-only source unexpectedly contains driving samples.' }
    if ($NativeObservationSource -and $nativeSamples -lt 10) { throw 'Native observation lacks original aligned raw/normalized/delivered samples.' }
    $sourceHash = Get-ControlHash $source
    $exit = Save-Companion $child
    $companionOutput = (Get-Content -LiteralPath (Join-Path $Result 'companion.stdout.txt') -Raw) +
        (Get-Content -LiteralPath (Join-Path $Result 'companion.stderr.txt') -Raw)
    if (!$NativeObservationSource) { Assert-OrdinaryNoDrivingRefusal $exit $companionOutput }
    $notice = Get-Content -LiteralPath (Join-Path $recording 'control/finalization.json') -Raw | ConvertFrom-Json
    if ($NativeObservationSource) {
        if ($notice.RequestId -ne $RequestId.ToString() -or $notice.State -notin @('saved','incomplete') -or
            (Get-ControlHash $source) -ne $sourceHash -or ($exit -eq 0) -ne ($notice.State -ceq 'saved')) { throw 'Native source finalization identity differs.' }
        $logText = [IO.File]::ReadAllText($files.log)
        if ($logText -notmatch 'Native observation phase=ended; captured-current-reader;' -or $logText -match 'Native observation phase=ended; (unavailable|refused):') { throw 'Native observation did not complete its first-reader interval.' }
        Copy-ControlExact $files.log (Join-Path $Result 'native-observation.log') (Get-ControlHash $files.log)
    } elseif ($notice.RequestId -ne $RequestId.ToString() -or $notice.State -cne 'incomplete' -or !$notice.Error -or
        (Get-ControlHash $source) -ne $sourceHash -or (Test-Path -LiteralPath (Join-Path $recording 'case.json')) -or
        (Test-Path -LiteralPath (Join-Path $recording 'force-observation.jsonl'))) { throw 'Menu-only no-driving finalization differs.' }
    Start-Sleep -Seconds 5
    Assert-OrdinaryGameExit $launched
    Closed
    Assert-StageRigLease -Path $slot -Token $RigLeaseToken
    $archive = Join-Path $Result 'recording-evidence'
    [IO.Directory]::CreateDirectory($archive) | Out-Null
    foreach ($name in @('source.jsonl','force-config.json','capture-profile.json','control/status.txt','control/finalization.json')) {
        $from = Join-Path $recording $name
        $hash = Get-ControlHash $from
        if ($hash -eq 'absent') { throw "Original recording evidence missing: $name" }
        Copy-ControlExact $from (Get-ControlChild $archive $name) $hash
    }
    [ordered]@{schema=1;result=$(if ($NativeObservationSource) { 'native-current-reader' } else { 'menu-only-incomplete' });nativeSamples=$nativeSamples;syntheticDriving=$false;requestId=$RequestId;sourceSha256=$sourceHash;companionExit=$exit;gamePid=$pidValue;gameStartUtcTicks=$ticks} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'smoke-result.json')
} catch {
    $errorText = $_.Exception.Message
    if (Test-Path -LiteralPath $Result) { $errorText | Set-Content -LiteralPath (Join-Path $Result 'run-error.txt') }
} finally {
    if ($mutated) {
        try {
            # The companion can still launch Woden after a startup failure. Both
            # actors must be finished, and no launch request may remain.
            Assert-OrdinaryCompanionStopped $child $requestPath
            if ($child -and !(Test-Path -LiteralPath (Join-Path $Result 'companion.stdout.txt'))) { $null = Save-Companion $child }
            $gameExit = Wait-OrdinaryGameExitForRestore $launched
            if ($gameExit) { $gameExit | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'game-exit-before-restore.json') }
            # Never mutate owner files under a live or unverified game process.
            Closed
            Assert-StageRigLease -Path $slot -Token $RigLeaseToken
            Restore-ControlFiles (Join-Path $Result 'owner-before') (Join-Path $Result 'candidate-after') $roots $files
            if ((Get-ControlHash (Join-Path $Result 'preferences.json')) -ne $prefsHash) { throw 'Preference snapshot changed.' }
            Registry-Restore (Join-Path $Result 'preferences.json')
            Assert-Inventory $before $roots $files
            Pin $receiptPath $OriginalReceiptSha256
            'restored' | Set-Content -LiteralPath (Join-Path $Result 'restored.txt')
            $restored = $true
        } catch {
            $_.Exception.Message | Set-Content -LiteralPath (Join-Path $Result 'restore-error.txt')
        }
    }
}
if (!$restored) { throw "Owner restoration not verified; retain caller-owned lease and inspect $Result. Run error: $errorText" }
if ($errorText) { throw "Smoke failed after verified restoration: $errorText" }
Write-Host "Ordinary diagnostic completed; owner state restored. Evidence: $Result"
