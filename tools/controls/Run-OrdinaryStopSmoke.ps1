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
    [Parameter(Mandatory)][Alias('Result')][string]$ResultDirectory,
    [Parameter(Mandatory)][string]$SteamExecutable,
    [Parameter(Mandatory)][string]$SteamLaunchHelper,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$SteamLaunchHelperSha256,
    [Parameter(Mandatory)][string]$ForegroundHelper,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ForegroundHelperSha256,
    [Parameter(Mandatory)][switch]$DesktopClientClick,
    [Parameter(Mandatory)][string]$UnityClientHelper,
    [Parameter(Mandatory)][string]$UnityClientHelperSha256,
    [Parameter(Mandatory)][string]$CaptureTool,
    [Parameter(Mandatory)][string]$CaptureToolSha256,
    [Parameter(Mandatory)][string]$CaptureAssemblySha256,
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
if (!$NativeObservationSource) { throw 'This shared-launch successor requires one explicit Native observation; no legacy activation fallback.' }
if ($SteamLaunchHelperSha256 -cne 'ECD5703B731C8E4A59C8BA84DB91B7490C4861DB1DC0B979B690EDA9DB708667' -or
    (Get-FileHash -LiteralPath $SteamLaunchHelper).Hash -cne $SteamLaunchHelperSha256) { throw 'Qualified shared launch helper changed.' }
. $SteamLaunchHelper
if ($ForegroundHelperSha256 -cne '3C12C250764748E649C41ACC56FF2D128C2AD09CA1D055021172BCBB7ED8EF7C' -or
    (Get-FileHash -LiteralPath $ForegroundHelper).Hash -cne $ForegroundHelperSha256) { throw 'Existing ordinary foreground helper changed.' }
. $ForegroundHelper
if ($UnityClientHelperSha256 -cne 'E25AEF755088D669912BC8EDC54BDB28483C2C7774A6DAC8370298A2C8F9D3A9' -or
    (Get-FileHash -LiteralPath $UnityClientHelper).Hash -cne $UnityClientHelperSha256 -or
    $CaptureToolSha256 -cne '2EDBC484C9610E382A54187F8E66801057A0A4E374CF241662B1D9142A0C2F64' -or
    (Get-FileHash -LiteralPath $CaptureTool).Hash -cne $CaptureToolSha256 -or
    $CaptureAssemblySha256 -cne 'D49AFD43A800B09C3A9BCAF7A93F9B5D206335F8AD90E71C4DD49F97A325377A' -or
    (Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($CaptureTool,'.dll'))).Hash -cne $CaptureAssemblySha256) { throw 'Shared client selector or qualified capture tool changed.' }
. $UnityClientHelper
if (!$DesktopClientClick) { throw 'This successor requires the announced screenshot-grounded operator client click; no programmatic activation fallback.' }

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
function Start-Companion([string]$Script,[string]$CaptureOutputRoot,[guid]$Id) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh).Source)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $casePrefix = if ($NativeObservationSource) { 'woden-native-observation-' } else { 'woden-menu-smoke-' }
    foreach ($arg in @('-NoProfile','-File',$Script,'-GameDir',$GameDir,'-RequestId',$Id.ToString(),'-CaseId',($casePrefix + $Id.ToString('N')),'-StartupTimeoutSeconds',$StartupTimeoutSeconds.ToString(),'-SessionTimeoutMinutes','5')) { [void]$start.ArgumentList.Add($arg) }
    if ($NativeObservationSource) {
        foreach ($arg in @('-ExternalLaunch','-InputOrigin','automated','-NativeObservationSource',$NativeObservationSource,'-NativeObservationSeconds',$NativeObservationSeconds.ToString())) { [void]$start.ArgumentList.Add($arg) }
    }
    $child = [Diagnostics.Process]::Start($start)
    if ($null -eq $child) { throw 'Installed companion did not start.' }
    $stamp=$child.StartTime.ToUniversalTime()
    Register-OrdinaryLaunchProcess ([pscustomobject]@{pid=$child.Id;startUtc=$stamp.ToString('O');startedUtcTicks=$stamp.Ticks;precision='Tick100ns';executable=$start.FileName}) $child 'companion'
    $stdout = $child.StandardOutput.ReadToEndAsync()
    $stderr = $child.StandardError.ReadToEndAsync()
    return [pscustomobject]@{process=$child;stdout=$stdout;stderr=$stderr;outputRoot=$CaptureOutputRoot}
}
function Save-Companion($Child) {
    $until=[DateTimeOffset]::UtcNow.AddSeconds(15)
    while (!$Child.process.WaitForExit(100)) { Observe-OrdinaryLaunch; if ([DateTimeOffset]::UtcNow -gt $until) { throw 'Companion did not finish after source completion; retain lease.' } }
    $Child.stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $Child.outputRoot 'companion.stdout.txt')
    $Child.stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $Child.outputRoot 'companion.stderr.txt')
    $Child.process.ExitCode | Set-Content -LiteralPath (Join-Path $Child.outputRoot 'companion.exit.txt')
    return $Child.process.ExitCode
}

$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$NativeCandidatePath = [IO.Path]::GetFullPath($NativeCandidatePath)
$NativeProvenancePath = [IO.Path]::GetFullPath($NativeProvenancePath)
$GameDir = [IO.Path]::GetFullPath($GameDir).TrimEnd('\','/')
$ResultDirectory = [IO.Path]::GetFullPath($ResultDirectory)
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
    steamAppId=(Join-Path $GameDir 'steam_appid.txt')
}

# These checks precede the first filesystem or game mutation. The 24A9 native
# and current unarmed automation package intentionally fail this gate.
if ($RequestId -eq [guid]::Empty -or $NativeSha256 -ne '088F1089BEB6D18D478F37582C59330C6BDA6BC37D5E365F6BA7EBF80040F567' -or
    $NativeProvenanceSha256 -ne '6068B7A848F85FE42AE88EE8A86B10F1EF4C4E307B9C4235BE44C1A84BBEA8E5') {
    throw 'Exact qualified x64 load-muted native and provenance are required.'
}
foreach ($path in @($PackageRoot,$NativeCandidatePath,$NativeProvenancePath,$GameDir,$ResultDirectory,$recording)) { Assert-ControlPlain $path }
if (Test-Path -LiteralPath $ResultDirectory) { throw 'Result must be a new directory.' }
if (Test-Path -LiteralPath $recording) { throw 'RequestId recording directory already exists.' }
if (Test-Path -LiteralPath $requestPath) { throw 'A one-shot ordinary recording request already exists.' }
if ($NativeObservationSource) {
    $NativeObservationSource = [IO.Path]::GetFullPath($NativeObservationSource)
    Assert-ControlPlain $NativeObservationSource
    if (!(Test-Path -LiteralPath (Join-Path $NativeObservationSource 'complete.tsv') -PathType Leaf)) { throw 'Sealed native observation reference required.' }
}
Closed
# This route uses the actual Steam library image. No disposable EXE or AppID-file writer.
$appManifest=Join-Path (Split-Path (Split-Path $GameDir -Parent) -Parent) 'appmanifest_3218630.acf'
Assert-ControlPlain $appManifest
$acf=[IO.File]::ReadAllText($appManifest)
$appIds=[regex]::Matches($acf,'(?m)^\s*"appid"\s*"([^"]+)"\s*$')
$installDirs=[regex]::Matches($acf,'(?m)^\s*"installdir"\s*"([^"]+)"\s*$')
if ($appIds.Count -ne 1 -or $appIds[0].Groups[1].Value -cne '3218630' -or $installDirs.Count -ne 1 -or
    ![string]::Equals([IO.Path]::GetFullPath((Join-Path (Split-Path $appManifest -Parent) ('common/'+$installDirs[0].Groups[1].Value))),$GameDir,[StringComparison]::OrdinalIgnoreCase)) { throw 'Woden must be the exact installed Steam library image.' }
if ((Test-Path -LiteralPath $files.steamAppId) -and [IO.File]::ReadAllText($files.steamAppId).Trim() -cne '3218630') { throw 'Existing owner AppID file differs; preserve it and refuse.' }
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

$sourceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$outputRoot=Join-Path $ResultDirectory 'launch-custody'
$observeExternal=$null
# Import the exact shared owned/identity implementation; no copied supervisor.
$ownedHelper='E:/Source/toolkits/dbce-wheel-mod-toolkit/tools/playback/Stage-OwnedProcess.ps1'
$identityHelper=Join-Path (Split-Path $ownedHelper -Parent) 'Stage-ProcessIdentity.ps1'
Pin $ownedHelper 'E58A8AE0B3096A3419196B491A9A773DBE80E45FC33CC41D819D108F513A0725'
Pin $identityHelper 'BE132A68893EE591DD76DE7143D41F5B87C767FEA6EB628952ABFEF477713A29'
. $ownedHelper
. (Join-Path $PSScriptRoot 'WodenLaunchCustody.ps1')
$ownedPrecision=[Collections.Generic.Dictionary[uint32,string]]::new()
$result=[ordered]@{schema='woden.shared-launch-custody@1';requestId=$RequestId.ToString('N');steps=@();identities=@();launchAttempted=$false;launchAdopted=$false;launchOutcome='not-requested';childrenQuiescent=$false;sharedHelperSha256=$SteamLaunchHelperSha256}
function Save-OrdinaryLaunchCustody {
    $result['ownedLineage']=@($owned.GetEnumerator() | ForEach-Object {[ordered]@{pid=$_.Key;createdUtc=$_.Value.ToString('O');precision=(Get-WodenOwnedCreationPrecision $_.Key $ownedPrecision)}})
    $path=Join-Path $outputRoot 'custody.json'
    $temp=$path+'.tmp-'+[guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temp,($result|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temp,$path,$true)
}
function Register-OrdinaryLaunchProcess($Record,$Process,[string]$Role) {
    # Retain first, then publish exact identity before token/readiness refusal.
    $ownedRoots.Add($Process)
    $stamp=[DateTime]::new([long]$Record.startedUtcTicks,[DateTimeKind]::Utc)
    if ($owned.ContainsKey([uint32]$Record.pid) -and $owned[[uint32]$Record.pid].Ticks -ne $stamp.Ticks) { throw 'Owned PID changed; retain custody.' }
    $owned[[uint32]$Record.pid]=$stamp
    $ownedPrecision[[uint32]$Record.pid]='Tick100ns'
    $result.identities+=@([ordered]@{pid=$Record.pid;createdUtc=$stamp.ToString('O');precision='Tick100ns';executable=$Record.executable;role=$Role})
    Save-OrdinaryLaunchCustody
    $null=$Process.SafeHandle
}
function Observe-OrdinaryLaunch {
    [void]@(Observe-Owned)
    Save-OrdinaryLaunchCustody
}
function Assert-OrdinaryLaunchClosed {
    Complete-Owned
    Save-OrdinaryLaunchCustody
    if (($result.launchAttempted -and !$result.launchAdopted) -or !$result.childrenQuiescent) { throw 'Shared launch outcome/descendants not closed; retain lease and owner state.' }
}
function Start-OrdinarySteamGame {
    Closed
    $context=Get-SteamLaunchContext -SteamExecutable $SteamExecutable
    $result['context']=$context; Save-OrdinaryLaunchCustody
    if ($context.decision -cnotin @('direct','broker')) { throw ('Shared Steam context refused: '+$context.reason) }
    $spec=@{Executable=$exe;WorkingDirectory=$GameDir;AppId='3218630';Environment=@{};AllowedEnvironmentNames=@()}
    # Child gets mandatory mute and both Steam IDs from the qualified helper.
    $started=[DateTimeOffset]::UtcNow
    if ($context.decision -ceq 'broker') {
        $broker=Join-Path $outputRoot 'launch-woden.cmd'
        $brokerHash=New-ChildEnvironmentBroker -Path $broker @spec
        $result['brokerPath']=$broker;$result['brokerSha256']=$brokerHash
        $result.launchAttempted=$true;$result.launchOutcome='unknown';Save-OrdinaryLaunchCustody
        try {
            $shell=Start-MediumBroker -Path $broker -BrokerSha256 $brokerHash -Context $context
            Register-OrdinaryLaunchProcess $shell.record $shell.process 'shell-request'
        } catch {
            $data=$_.Exception.Data
            if ($data.Contains('SteamShellRequestAttempted') -and !$data['SteamShellRequestAttempted']) { $result.launchAttempted=$false;$result.launchOutcome='not-requested' }
            if ($data.Contains('ShellRequestRecord') -and $data.Contains('ShellRequestProcess')) { Register-OrdinaryLaunchProcess $data['ShellRequestRecord'] $data['ShellRequestProcess'] 'shell-request' }
            Save-OrdinaryLaunchCustody;throw
        }
    } else {
        $start=New-SteamChildStartInfo @spec
        $result.launchAttempted=$true;$result.launchOutcome='unknown';Save-OrdinaryLaunchCustody
        $game=[Diagnostics.Process]::Start($start)
        if (!$game) { throw 'Direct game start returned no process; retain unknown launch custody.' }
        $stamp=$game.StartTime.ToUniversalTime()
        $script:launched=$game
        Register-OrdinaryLaunchProcess ([pscustomobject]@{pid=$game.Id;startedUtcTicks=$stamp.Ticks;executable=$exe}) $game 'direct-game'
    }
    $adopted=Wait-SteamLaunchProcess -Executable $exe -LaunchStartedUtc $started -Context $context -OnAdopted {
        param($record,$process)
        $script:launched=$process
        Register-OrdinaryLaunchProcess $record $process 'game'
    }
    $result.launchAdopted=$true;$result.launchOutcome=$adopted.launchOutcome
    $result['game']=$adopted.record;Save-OrdinaryLaunchCustody
    if (!$adopted.record.integrityMatchesSteam) { throw ('Game token did not match Steam: '+$adopted.record.integrityError) }
    # Only observe using the existing canonical native declarations. Desktop
    # interaction belongs to the announced operator's screenshot-grounded sky
    # click, not a second SetForegroundWindow request from this supervisor.
    if ([DateTime]::UtcNow -ge $startupDeadline) { throw 'Original Native startup deadline elapsed before operator admission.' }
    Initialize-StageForegroundNative
    $focusDeadline = [DateTime]::UtcNow.AddSeconds(45)
    if ($focusDeadline -gt $startupDeadline) { $focusDeadline = $startupDeadline }
    $result['foreground'] = [ordered]@{status='awaiting-desktop-client-click';requested=$null;requestAccepted=$null;osForegroundObserved=$false;window=0L;foregroundWindow=0L;foregroundProcessId=0;processId=$adopted.record.pid;startedUtcTicks=$adopted.record.startedUtcTicks;createdUtc=$adopted.record.startUtc;executablePath=$exe;deadlineUtc=$focusDeadline.ToString('O');inputProof='separate operator transcript required';observations=0}
    $result['foregroundHelperSha256'] = $ForegroundHelperSha256
    Save-OrdinaryLaunchCustody
    do {
        if ([DateTime]::UtcNow -ge $focusDeadline) { break }
        Assert-StageRigLease -Path $slot -Token $RigLeaseToken
        $launched.Refresh()
        if ($launched.HasExited) { throw 'Recorded game exited before operator foreground admission.' }
        Assert-StageForegroundIdentity -Actual ([pscustomobject]@{Id=$launched.Id;StartedUtcTicks=$launched.StartTime.ToUniversalTime().Ticks;ExecutablePath=[string]$launched.Path}) `
            -ProcessId $adopted.record.pid -StartedUtcTicks $adopted.record.startedUtcTicks -ExecutablePath $exe
        if (!$result.foreground.window) {
            # Existing capture --list is itself owned by the unchanged canonical
            # child boundary; it records process start and OS window class/owner.
            if (($focusDeadline - [DateTime]::UtcNow).TotalSeconds -lt 6) { break }
            $listName='unity-client-list-'+$result.steps.Count
            $result.steps+=@([ordered]@{name=$listName;state='started';startedUtc=[DateTime]::UtcNow.ToString('O')})
            try { Invoke-OwnedProcess $listName $CaptureTool @([string]$adopted.record.pid,'--list') 5 }
            finally {
                if ($result.steps[-1].process) { $ownedPrecision[[uint32]$result.steps[-1].process.pid]='Tick100ns' }
                $result.steps[-1].endedUtc=[DateTime]::UtcNow.ToString('O');Save-OrdinaryLaunchCustody
            }
            $listPath=Join-Path $outputRoot ($listName+'.stdout.log')
            $windowList=Get-Content -LiteralPath $listPath -Raw|ConvertFrom-Json -DateKind String
            $client=Select-StageUnityGameClient -WindowList $windowList -Process $launched `
                -ProcessId $adopted.record.pid -StartedUtcTicks $adopted.record.startedUtcTicks `
                -ExecutablePath $exe -ExpectedWindowHandle $result.foreground.window
            $result.foreground['clientSelection']=$client
            $result.foreground['windowListPath']=$listPath
            $result.foreground['windowListSha256']=(Get-FileHash -LiteralPath $listPath).Hash
            $result.foreground['selectorSha256']=$UnityClientHelperSha256
            if ($client.state -ceq 'selected') {
                $result.foreground.status='awaiting-desktop-client-click'
                $result.foreground.window=$client.window
                $result.foreground.foregroundWindow=$client.foregroundWindow
                $result.foreground.foregroundProcessId=$client.foregroundProcessId
                $result.foreground.osForegroundObserved=$client.osForegroundObserved
            } else { $result.foreground.status='waiting-for-unity-client' }
        } else {
            # Keep the selected Unity HWND; foreground polling does not spawn
            # another enumerator after renderer/class selection succeeded.
            $window=[IntPtr]$result.foreground.window
            [uint32]$windowProcess=0
            if (![Dbce.StageForegroundNative]::GetWindowThreadProcessId($window,[ref]$windowProcess) -or
                $windowProcess -ne $adopted.record.pid -or ![Dbce.StageForegroundNative]::IsWindowVisible($window) -or
                [Dbce.StageForegroundNative]::IsIconic($window)) { throw 'Selected Unity client ownership/visibility/minimized state changed.' }
            $foreground=[Dbce.StageForegroundNative]::GetForegroundWindow()
            [uint32]$foregroundProcess=0
            [void][Dbce.StageForegroundNative]::GetWindowThreadProcessId($foreground,[ref]$foregroundProcess)
            $result.foreground.foregroundWindow=$foreground.ToInt64()
            $result.foreground.foregroundProcessId=$foregroundProcess
            $result.foreground.osForegroundObserved=$foreground -eq $window -and $foregroundProcess -eq $adopted.record.pid
            $result.foreground['foregroundCheckedUtc']=[DateTime]::UtcNow.ToString('O')
        }
        $result.foreground.observations++
        Observe-OrdinaryLaunch
        if ($result.foreground.osForegroundObserved -and [DateTime]::UtcNow -lt $focusDeadline) { break }
        $result.foreground.osForegroundObserved=$false
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $focusDeadline)
    if (!$result.foreground.osForegroundObserved) {
        $result.foreground.status='unmet';Save-OrdinaryLaunchCustody
        throw 'Operator client click did not establish exact OS foreground within the original startup bound.'
    }
    $result.foreground.status='observed';$result.foreground.observedUtc=[DateTime]::UtcNow.ToString('O');Save-OrdinaryLaunchCustody
}

$mutated = $false
$restored = $false
$launched = $null
$child = $null
$errorText = $null
try {
    [IO.Directory]::CreateDirectory($ResultDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    Save-OrdinaryLaunchCustody
    Save-ControlFiles (Join-Path $ResultDirectory 'owner-before') $roots $files
    Registry-Snapshot (Join-Path $ResultDirectory 'preferences.json')
    $before = Get-Content -LiteralPath (Join-Path $ResultDirectory 'owner-before/manifest.json') -Raw | ConvertFrom-Json
    $prefsHash = Get-ControlHash (Join-Path $ResultDirectory 'preferences.json')
    [ordered]@{schema=1;gameDir=$GameDir;requestId=$RequestId;packageManifestSha256=$PackageManifestSha256;nativeSha256=$NativeSha256;nativeProvenanceSha256=$NativeProvenanceSha256;originalReceiptSha256=$OriginalReceiptSha256;leasePath=$slot;ownerBackupSha256=(Get-ControlHash (Join-Path $ResultDirectory 'owner-before/manifest.json'));preferencesSha256=$prefsHash} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ResultDirectory 'recovery.json')
    $mutated = $true
    & pwsh -NoProfile -File (Join-Path $PackageRoot 'Manage-Install.ps1') -Mode Install -PackageRoot $PackageRoot -GameDir $GameDir *> (Join-Path $ResultDirectory 'install.log')
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
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultDirectory 'telemetry-delivery-mute.json')
    Assert-StageRigLease -Path $slot -Token $RigLeaseToken
    Closed
    $startupDeadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $child = Start-Companion $companionPath $ResultDirectory $RequestId
    $requestDeadline=[DateTimeOffset]::UtcNow.AddSeconds(15)
    while (!(Test-Path -LiteralPath $requestPath)) {
        Observe-OrdinaryLaunch
        if ($child.process.HasExited -or [DateTimeOffset]::UtcNow -gt $requestDeadline) { throw 'External-launch companion did not prepare the request.' }
        Start-Sleep -Milliseconds 100
    }
    $prepared=Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
    if ($prepared.Id -ne $RequestId.ToString() -or $prepared.DisableForces -ne $true -or $prepared.InputOrigin -cne 'automated') { throw 'Prepared request identity/mute differs; no launch.' }
    Start-OrdinarySteamGame
    $statusPath = Join-Path $recording 'control/status.txt'
    $deadline = $startupDeadline
    do {
        $status = Status $statusPath
        if ($status -and $status.id -ceq $idText -and $status.status -ceq 'Recording' -and $status.active -ceq 'True') { break }
        if ($child.process.HasExited) { throw 'Installed companion exited before Recording status.' }
        Observe-OrdinaryLaunch
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    if (!$status -or $status.status -cne 'Recording') { throw 'Timed out waiting for exact Recording status.' }
    $pidValue = 0; $ticks = [long]0
    if (![int]::TryParse($status.pid,[ref]$pidValue) -or ![long]::TryParse($status.startedUtcTicks,[ref]$ticks)) { throw 'Recorder process identity is invalid.' }
    if (!$launched -or $launched.Id -ne $pidValue) { throw 'Recorder differs from the already adopted exact game.' }
    if ($launched.StartTime.ToUniversalTime().Ticks -ne $ticks -or ![string]::Equals($launched.Path,$exe,[StringComparison]::OrdinalIgnoreCase) -or
        @(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue).Count -ne 1) { throw 'Exact Woden process identity differs.' }
    if (Test-Path -LiteralPath $requestPath) {
        $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
        if ($request.Id -ne $RequestId.ToString() -or $request.DisableForces -ne $true) { throw 'One-shot request did not demand force disablement.' }
    }
    if (!$NativeObservationSource) {
        Start-Sleep -Seconds $MenuDwellSeconds
        & pwsh -NoProfile -File $companionPath -GameDir $GameDir -Stop -RequestId $RequestId *> (Join-Path $ResultDirectory 'stop.log')
        if ($LASTEXITCODE -ne 0) { throw "Installed request-bound Stop refused: $LASTEXITCODE" }
    }
    # Native observation owns its bounded first-reader interval and Stop/normal exit; never inject raw driving here.
    $deadline = [DateTime]::UtcNow.AddSeconds($(if ($NativeObservationSource) { 210 } else { 20 }))
    do {
        $status = Status $statusPath
        if ($status -and $status.id -ceq $idText -and $status.status -ceq 'Completed' -and $status.active -ceq 'False') { break }
        Observe-OrdinaryLaunch
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
    $companionOutput = (Get-Content -LiteralPath (Join-Path $ResultDirectory 'companion.stdout.txt') -Raw) +
        (Get-Content -LiteralPath (Join-Path $ResultDirectory 'companion.stderr.txt') -Raw)
    if (!$NativeObservationSource) { Assert-OrdinaryNoDrivingRefusal $exit $companionOutput }
    $notice = Get-Content -LiteralPath (Join-Path $recording 'control/finalization.json') -Raw | ConvertFrom-Json
    if ($NativeObservationSource) {
        if ($notice.RequestId -ne $RequestId.ToString() -or $notice.State -notin @('saved','incomplete') -or
            (Get-ControlHash $source) -ne $sourceHash -or ($exit -eq 0) -ne ($notice.State -ceq 'saved')) { throw 'Native source finalization identity differs.' }
        $logText = [IO.File]::ReadAllText($files.log)
        if ($logText -notmatch 'Native observation phase=ended; captured-current-reader;' -or $logText -match 'Native observation phase=ended; (unavailable|refused):') { throw 'Native observation did not complete its first-reader interval.' }
        Copy-ControlExact $files.log (Join-Path $ResultDirectory 'native-observation.log') (Get-ControlHash $files.log)
    } elseif ($notice.RequestId -ne $RequestId.ToString() -or $notice.State -cne 'incomplete' -or !$notice.Error -or
        (Get-ControlHash $source) -ne $sourceHash -or (Test-Path -LiteralPath (Join-Path $recording 'case.json')) -or
        (Test-Path -LiteralPath (Join-Path $recording 'force-observation.jsonl'))) { throw 'Menu-only no-driving finalization differs.' }
    Start-Sleep -Seconds 5
    Assert-OrdinaryGameExit $launched
    Closed
    Assert-StageRigLease -Path $slot -Token $RigLeaseToken
    $archive = Join-Path $ResultDirectory 'recording-evidence'
    [IO.Directory]::CreateDirectory($archive) | Out-Null
    foreach ($name in @('source.jsonl','force-config.json','capture-profile.json','control/status.txt','control/finalization.json')) {
        $from = Join-Path $recording $name
        $hash = Get-ControlHash $from
        if ($hash -eq 'absent') { throw "Original recording evidence missing: $name" }
        Copy-ControlExact $from (Get-ControlChild $archive $name) $hash
    }
    [ordered]@{schema=1;result=$(if ($NativeObservationSource) { 'native-current-reader' } else { 'menu-only-incomplete' });nativeSamples=$nativeSamples;syntheticDriving=$false;requestId=$RequestId;sourceSha256=$sourceHash;companionExit=$exit;gamePid=$pidValue;gameStartUtcTicks=$ticks} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultDirectory 'smoke-result.json')
} catch {
    $errorText = $_.Exception.Message
    if (Test-Path -LiteralPath $ResultDirectory) { $errorText | Set-Content -LiteralPath (Join-Path $ResultDirectory 'run-error.txt') }
} finally {
    if ($mutated) {
        try {
            # The companion can still launch Woden after a startup failure. Both
            # actors must be finished, and no launch request may remain.
            Assert-OrdinaryCompanionStopped $child $requestPath
            if ($child -and !(Test-Path -LiteralPath (Join-Path $ResultDirectory 'companion.stdout.txt'))) { $null = Save-Companion $child }
            $gameExit = Wait-OrdinaryGameExitForRestore $launched
            if ($gameExit) { $gameExit | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultDirectory 'game-exit-before-restore.json') }
            # A shell timeout/late child can outlive recorder readiness. Never infer absence.
            Assert-OrdinaryLaunchClosed
            # Never mutate owner files under a live or unverified game process.
            Closed
            Assert-StageRigLease -Path $slot -Token $RigLeaseToken
            Restore-ControlFiles (Join-Path $ResultDirectory 'owner-before') (Join-Path $ResultDirectory 'candidate-after') $roots $files
            if ((Get-ControlHash (Join-Path $ResultDirectory 'preferences.json')) -ne $prefsHash) { throw 'Preference snapshot changed.' }
            Registry-Restore (Join-Path $ResultDirectory 'preferences.json')
            Assert-Inventory $before $roots $files
            Pin $receiptPath $OriginalReceiptSha256
            'restored' | Set-Content -LiteralPath (Join-Path $ResultDirectory 'restored.txt')
            $restored = $true
        } catch {
            $_.Exception.Message | Set-Content -LiteralPath (Join-Path $ResultDirectory 'restore-error.txt')
        }
    }
}
if (!$restored) { throw "Owner restoration not verified; retain caller-owned lease and inspect $ResultDirectory. Run error: $errorText" }
if ($errorText) { throw "Smoke failed after verified restoration: $errorText" }
Write-Host "Ordinary diagnostic completed; owner state restored. Evidence: $ResultDirectory"
