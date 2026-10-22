#Requires -Version 7.0
# Supervised real-drive recording and cold-launch offline replay. The adapter owns menus/physics; this runner owns
# the process deadline, evidence and exact local owner-state restoration.
[CmdletBinding()]
param(
    [switch]$Record,
    [ValidateRange(10,1800)][int]$Seconds = 60,
    [string]$Recording,
    # An admitted ordinary package (TelemetryInspector admit): reviewed with ordinary-stage-review instead of the
    # muted-capture stage-review; its nested stage is replayed. Playback stays muted either way.
    [string]$AdmittedPackage,
    [ValidatePattern('^[a-fA-F0-9]{32}$')][string]$RequestId,
    [ValidatePattern('^[a-fA-F0-9]{64}$')][string]$PackageSealSha256,
    [switch]$SpanSeparateMonitors,
    [Parameter(Mandatory)][string]$Result,
    [string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge',
    [ValidateRange(240,2400)][int]$TimeoutSeconds = 360,
    [switch]$RestoreOnly,
    [string]$RigLeaseToken,
    [switch]$CheckEnvironment
)
$ErrorActionPreference = 'Stop'
$slot = Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
$rigLease = $null
$borrowedLease = $false
function Runner-File([string]$Name,[string]$SourceRelative) {
    $installed = Join-Path $PSScriptRoot $Name
    if (Test-Path -LiteralPath $installed -PathType Leaf) { return $installed }
    $source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $SourceRelative))
    if (Test-Path -LiteralPath $source -PathType Leaf) { return $source }
    throw "Missing runner helper: $Name"
}
$rigScript = Runner-File 'Stage-RigLease.ps1' '../../vendor/playback/Stage-RigLease.ps1'
$rigPin = Get-Content (Join-Path (Split-Path $rigScript -Parent) 'rig-lease-provenance.json') -Raw | ConvertFrom-Json
if ($rigPin.schema -ne 'dbce.stage-rig-lease-pin@1' -or (Get-FileHash -LiteralPath $rigScript).Hash -ine $rigPin.sha256) { throw 'Rig lease source pin differs' }
. $rigScript
$lifecycle = Runner-File 'Stage-RunnerLifecycle.ps1' '../../vendor/playback/Stage-RunnerLifecycle.ps1'
if ((Get-FileHash -LiteralPath $lifecycle).Hash -ine '4BA9A38519C981E4B9D7CF3C9765C9DCB0ECB4C15F3720CDB197C6DF4A27F62C') { throw 'Runner lifecycle source pin differs' }
. $lifecycle
. (Runner-File 'ReplayEnvironment.ps1' 'ReplayEnvironment.ps1')
# PowerShell's location can differ from the process CWD (especially Start-Job).
# Resolve caller paths here before passing them to .NET or the native inspector.
$Result = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Result)
$GameDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GameDir)
if ($Recording) { $Recording = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Recording) }
if ($AdmittedPackage) { $AdmittedPackage = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($AdmittedPackage) }
if ($AdmittedPackage -and $Result.StartsWith($AdmittedPackage.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Result must stay outside the admitted package.'
}
if (($RequestId -and (!$AdmittedPackage -or !$PackageSealSha256)) -or ($PackageSealSha256 -and !$RequestId)) {
    throw 'RequestId and PackageSealSha256 require an admitted playback package together.'
}
if ($RequestId -and ($Record -or $Recording -or $RestoreOnly -or $CheckEnvironment -or $SpanSeparateMonitors)) {
    throw 'A pinned playback request cannot record, restore or change display settings.'
}
if ($RequestId) { $RequestId = $RequestId.ToLowerInvariant(); $PackageSealSha256 = $PackageSealSha256.ToLowerInvariant() }
$control = Join-Path $env:LOCALAPPDATA 'Dbce/StagePlayback/woden'
$data = Join-Path $env:USERPROFILE 'AppData/LocalLow/ViJuDa/Super Woden Rally Edge'
$backup = Join-Path $Result 'owner-before'
$prefsKey = 'HKCU:\Software\ViJuDa\Super Woden Rally Edge'
$roots = [ordered]@{ config = Join-Path $GameDir 'BepInEx/config'; save = Join-Path $data 'Save'; controllers = Join-Path $data 'ControllerScheme' }
function Assert-Closed {
    if (Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue) { throw 'Close Super Woden Rally Edge before launching or restoring.' }
}
function Owner-Files {
    foreach ($entry in $roots.GetEnumerator()) {
        if (!(Test-Path -LiteralPath $entry.Value)) { continue }
        if ((Get-Item -LiteralPath $entry.Value).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Owner directory is a link.' }
        foreach ($file in Get-ChildItem -LiteralPath $entry.Value -Recurse -Force) {
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Owner path contains a link.' }
            if ($file.PSIsContainer -or $file.Name -like '*.install.json') { continue }
            [pscustomobject]@{ key = $entry.Key + '/' + $file.FullName.Substring($entry.Value.Length+1).Replace('\','/'); path = $file.FullName }
        }
    }
}
function Child([string]$Parent, [string]$Relative) {
    $Parent = [IO.Path]::GetFullPath($Parent)
    $full = [IO.Path]::GetFullPath((Join-Path $Parent $Relative))
    if (!$full.StartsWith($Parent.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $Relative.Contains(':')) { throw 'Invalid backup relative path.' }
    return $full
}
function Copy-Exact([string]$From,[string]$To) {
    [IO.Directory]::CreateDirectory((Split-Path -Parent $To)) | Out-Null
    Copy-Item -LiteralPath $From -Destination $To -Force
    if ((Get-FileHash -LiteralPath $From).Hash -ne (Get-FileHash -LiteralPath $To).Hash) { throw 'Copy verification failed.' }
}
function Read-Status([string]$Path) {
    $stream = $null; $reader = $null
    try {
        $stream = [IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $reader = [IO.StreamReader]::new($stream)
        return $reader.ReadToEnd() -split '\r?\n'
    } catch [IO.IOException] { return @() }
    finally { if ($reader) { $reader.Dispose() } elseif ($stream) { $stream.Dispose() } }
}
function Set-PreparedReplayIdentity([string[]]$Lines,[string]$RequestedId) {
    $guid=[Guid]::Empty
    if(![Guid]::TryParseExact($RequestedId,'N',[ref]$guid)){throw 'Requested replay ID is invalid.'}
    $fields=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach($line in $Lines){$eq=$line.IndexOf('=');if($eq -le 0 -or $line -match '[\r\n]' -or !$fields.TryAdd($line.Substring(0,$eq),$line.Substring($eq+1))){throw 'Prepared replay request is malformed.'}}
    if($fields.Count -ne 5 -or !$fields.ContainsKey('id') -or ![Guid]::TryParseExact($fields['id'],'N',[ref]$guid) -or
        !$fields.ContainsKey('action') -or $fields['action'] -cne 'replay' -or !$fields.ContainsKey('expiresUtc') -or
        !$fields.ContainsKey('path') -or ![IO.Path]::IsPathFullyQualified($fields['path']) -or
        !$fields.ContainsKey('output') -or ![IO.Path]::IsPathFullyQualified($fields['output'])){throw 'Prepared request is not the expected replay.'}
    foreach($line in $Lines){if($line.StartsWith('id=',[StringComparison]::Ordinal)){'id='+$RequestedId.ToLowerInvariant()}else{$line}}
}
function Review-Recording([string]$Path) {
    $inspector = Join-Path $PSScriptRoot 'WodenCaseFinalizer.exe'
    if (!(Test-Path -LiteralPath $inspector)) { $inspector = Join-Path $PSScriptRoot '../../components/wheel/tools/TelemetryInspector/bin/Release/net10.0/TelemetryInspector.dll' }
    if (!(Test-Path -LiteralPath $inspector)) { throw 'Build TelemetryInspector before running a stage session.' }
    $report = if ($inspector.EndsWith('.exe',[StringComparison]::OrdinalIgnoreCase)) { & $inspector stage-review $Path } else { & dotnet $inspector stage-review $Path }
    if ($LASTEXITCODE -ne 0) { throw 'Original recording validation failed.' }
    return ($report -join [Environment]::NewLine)
}
function Review-AdmittedPackage([string]$Path) {
    $inspector = Join-Path $PSScriptRoot 'WodenCaseFinalizer.exe'
    if (!(Test-Path -LiteralPath $inspector)) { $inspector = Join-Path $PSScriptRoot '../../components/wheel/tools/TelemetryInspector/bin/Release/net10.0/TelemetryInspector.dll' }
    if (!(Test-Path -LiteralPath $inspector)) { throw 'Build TelemetryInspector before running a stage session.' }
    $report = if ($inspector.EndsWith('.exe',[StringComparison]::OrdinalIgnoreCase)) { & $inspector ordinary-stage-review $Path } else { & dotnet $inspector ordinary-stage-review $Path }
    if ($LASTEXITCODE -ne 0) { throw 'Admitted ordinary package validation failed.' }
    return ($report -join [Environment]::NewLine)
}
function Write-TerminalResult {
    if (!$RequestId -or !$resultMayWrite) { return }
    if (!(Test-Path -LiteralPath $Result)) { [IO.Directory]::CreateDirectory($Result) | Out-Null }
    $restored = Test-Path -LiteralPath (Join-Path $Result 'restored.txt') -PathType Leaf
    $closed = $null -ne $exitEvidence -and $exitEvidence.exited
    $exitCode = if ($null -ne $exitEvidence) { $exitEvidence.exitCode } else { $null }
    $passed = $sessionSucceeded -and $restored -and $outputsMuted -and $closed -and $exitCode -eq 0
    $message = if ($passed) { 'Muted cold replay passed; owner state restored.' } elseif ($runFailure) { $runFailure } else { 'Muted cold replay did not qualify.' }
    $message = ([string]$message -replace '[\r\n\x00-\x1f]+',' ').Trim()
    if ($message.Length -gt 512) { $message = $message.Substring(0,512).TrimEnd() }
    $document = [ordered]@{
        schema='woden.full-session-result@1'; requestId=$RequestId; action='playback'; recording=$AdmittedPackage
        sessionSha256=$admittedSessionSha; packageSealSha256=$verifiedPackageSeal; stageSealSha256=$admittedSeal
        passed=[bool]$passed; ownerRestored=[bool]$restored; outputsMuted=[bool]$outputsMuted; closed=[bool]$closed
        gameExitCode=$exitCode; completedUtc=[DateTime]::UtcNow.ToString('o'); message=$message
    }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($document | ConvertTo-Json -Depth 5 -Compress))
    if ($bytes.Length -gt 65536) { throw 'Terminal result exceeds 64 KiB.' }
    $stream = [IO.FileStream]::new((Join-Path $Result 'result.json'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
function Get-ObservedGameExitEvidence([object]$Process,[IntPtr]$ObservedHandle) {
    if ($ObservedHandle -eq [IntPtr]::Zero) { throw 'The exact game process handle was unavailable while alive.' }
    $exited = $false; $exitCode = $null
    try {
        $exited = $Process.HasExited
        if ($exited) { $exitCode = $Process.ExitCode } # Preserve null; never coerce an unavailable code to zero.
    } catch { $exited = $false; $exitCode = $null }
    $evidence = New-StageGameExitEvidence -CloseMainWindowRequested:$false -Exited:$exited -ExitCode $exitCode -ProcessId $Process.Id
    $evidence | Add-Member -NotePropertyName handleObservedWhileAlive -NotePropertyValue $true
    return $evidence
}
function Save-Owner {
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    $files = @(foreach ($file in Owner-Files) {
        $destination = Child $backup $file.key
        Copy-Exact $file.path $destination
        $hash = (Get-FileHash -LiteralPath $file.path).Hash
        if ((Get-FileHash -LiteralPath $destination).Hash -ne $hash) { throw 'Owner file changed during backup.' }
        @{ key = $file.key; sha256 = $hash }
    })
    $prefs = @((Get-Item -LiteralPath $prefsKey).GetValueNames() | ForEach-Object { Get-SessionRegistryValue $_ })
    ConvertTo-Json -InputObject $prefs -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'preferences.json') -Encoding utf8
    @{ schema=1; game=$GameDir; files=$files; prefsHash=(Get-FileHash (Join-Path $backup 'preferences.json')).Hash } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $backup 'manifest.json') -Encoding utf8
}
function Restore-Owner {
    Assert-Closed
    $manifest = Get-Content -LiteralPath (Join-Path $backup 'manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or $manifest.game -ine $GameDir) { throw 'Wrong backup schema/game path.' }
    $prefsPath = Join-Path $backup 'preferences.json'
    if ((Get-FileHash -LiteralPath $prefsPath).Hash -ne $manifest.prefsHash) { throw 'Preference backup changed.' }
    $prefs = @(Get-Content -LiteralPath $prefsPath -Raw | ConvertFrom-Json)
    foreach ($value in $prefs) { $null = [Convert]::FromBase64String($value.data) }
    $destinations = @{}
    foreach ($file in $manifest.files) {
        $parts = $file.key -split '/',2
        if ($parts.Count -ne 2 -or !$roots.Contains($parts[0])) { throw 'Unknown owner scope.' }
        $saved = Child $backup $file.key
        if ((Get-FileHash -LiteralPath $saved).Hash -ne $file.sha256) { throw ('Backup changed: '+$file.key) }
        $destinations[$file.key] = Child $roots[$parts[0]] $parts[1]
    }
    # Enumerate and validate every current path before changing any file.
    $current = @(Owner-Files)
    foreach ($file in $current) {
        $post = Child (Join-Path $Result 'owner-after') $file.key
        Copy-Exact $file.path $post
        if (!$destinations.ContainsKey($file.key)) { Remove-Item -LiteralPath $file.path }
    }
    foreach ($file in $manifest.files) {
        Copy-Exact (Child $backup $file.key) $destinations[$file.key]
        if ((Get-FileHash -LiteralPath $destinations[$file.key]).Hash -ne $file.sha256) { throw 'File restoration readback failed.' }
    }
    foreach ($name in (Get-Item -LiteralPath $prefsKey).GetValueNames()) {
        if ($name -notin $prefs.name) { Remove-ItemProperty -LiteralPath $prefsKey -Name $name }
    }
    foreach ($value in $prefs) { Set-SessionRegistryValue $value }
    foreach ($value in $prefs) {
        $actual = Get-SessionRegistryValue $value.name
        if ($actual.type -ne $value.type -or $actual.data -cne $value.data) { throw 'Preference restoration readback failed.' }
    }
    [DateTime]::UtcNow.ToString('O') | Set-Content -LiteralPath (Join-Path $Result 'restored.txt')
}
$resultMayWrite = [bool]$RequestId -and !(Test-Path -LiteralPath $Result)
$runFailure = $null; $sessionSucceeded = $false; $outputsMuted = $false
$admittedSessionSha = $null; $verifiedPackageSeal = $null; $admittedSeal = $null; $exitEvidence = $null
$gameProcess = $null; $gameHandle = [IntPtr]::Zero
$lease = [Threading.Mutex]::new($false,'Local\DbceStageReplayRunner')
$held = $false
try {
    Assert-Closed
    try { $held = $lease.WaitOne(0) } catch [Threading.AbandonedMutexException] { $held = $true }
    if (!$held) { throw 'Another replay runner owns the test slot.' }
    if ($RestoreOnly) {
        $savedLease = Join-Path $Result 'rig-lease.json'
        if (Test-Path -LiteralPath $savedLease) {
            $restoredLease = Get-Content -LiteralPath $savedLease -Raw | ConvertFrom-Json
            if ($restoredLease.path -ine $slot) { throw 'Recovery rig lease path differs' }
            if ($restoredLease.borrowed) {
                if (!$RigLeaseToken -or $RigLeaseToken -cne $restoredLease.token) { throw 'Borrowed recovery requires the exact outer rig lease token.' }
                Assert-StageRigLease -Path $slot -Token $RigLeaseToken
            } else {
                if ($RigLeaseToken) { throw 'An owned recovery lease cannot be borrowed.' }
            }
        } elseif ($RigLeaseToken) {
            throw 'Borrowed recovery has no saved rig lease identity.'
        }
        Restore-Owner
        if ($restoredLease -and !$restoredLease.borrowed) { $null = Exit-StageRigLease $restoredLease }
        Write-Output 'Owner files and preferences restored and verified.'; return
    }
    if (Test-Path -LiteralPath $Result) { throw 'Choose a new result directory.' }
    if ($CheckEnvironment) {
        if ($RigLeaseToken) { Assert-StageRigLease -Path $slot -Token $RigLeaseToken }
        Save-Owner; Restore-Owner; Write-Output 'PASS: owner environment backup and exact readback.'; return
    }
    if ($AdmittedPackage -and ($Record -or $Recording)) { throw 'Choose either an admitted package, Record or a completed Recording.' }
    if ($AdmittedPackage) {
        # Play exactly the stage the admitted review validated, pinned by its seal (Rita 72210).
        $admittedReview = Review-AdmittedPackage $AdmittedPackage
        $admitted = $admittedReview | ConvertFrom-Json
        $Recording = Child $AdmittedPackage ([string]$admitted.stage)
        $admittedSeal = ([string]$admitted.stageSealSha256).ToLowerInvariant()
        if ($admittedSeal -notmatch '^[0-9a-f]{64}$') { throw 'Admitted review did not pin its stage seal.' }
        $admittedSessionSha = ([string]$admitted.sessionSha256).ToLowerInvariant()
        $verifiedPackageSeal = ([string]$admitted.packageSealSha256).ToLowerInvariant()
        if ($admittedSessionSha -notmatch '^[0-9a-f]{64}$' -or $verifiedPackageSeal -notmatch '^[0-9a-f]{64}$') { throw 'Admitted review lacks exact package hashes.' }
        if ($RequestId -and $verifiedPackageSeal -cne $PackageSealSha256) { throw 'The admitted outer package seal differs from the pinned request.' }
    }
    if ($Record -and $Recording) { throw 'Choose either Record or a completed Recording.' }
    if ($Record -and $SpanSeparateMonitors) { throw 'The temporary span override is for replay qualification only.' }
    if (!$Record -and (!$Recording -or !(Test-Path -LiteralPath (Join-Path $Recording 'complete.tsv')))) { throw 'A completed recording is required.' }
    if (!$Record) { $sourceReview = if ($AdmittedPackage) { $admittedReview } else { Review-Recording $Recording } }
    if ($Record) { $Recording = Join-Path $Result 'recording' }
    if (Test-Path -LiteralPath (Join-Path $control 'request.txt')) { throw 'An unconsumed stage request exists.' }
    $other = Get-Process -Name 'artofrally','iracing-arcade','DRIVERally','SonicRacingCrossWorldsSteam','OR2006C2C' -ErrorAction SilentlyContinue
    if ($other) { throw 'Another game owns the test slot.' }
    if ($RigLeaseToken) {
        Assert-StageRigLease -Path $slot -Token $RigLeaseToken
        $rigLease = [pscustomobject]@{path=$slot;token=$RigLeaseToken;borrowed=$true}
        $borrowedLease = $true
    } else {
        $rigLease = Enter-StageRigLease -Path $slot -Owner 'codex woden-rally-edge' -Purpose 'supervised stage session'
    }
    $command = Join-Path $GameDir 'BepInEx/plugins/WodenRallyEdgeWheel/Stage-Session.ps1'
    Save-Owner
    $rigLease | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Result 'rig-lease.json') -Encoding utf8
    if ($SpanSeparateMonitors) {
        Enable-SessionSpan (Join-Path $GameDir 'BepInEx/config/dbce.wodenrallyedgewheel.cfg')
        'Triple.SpanSeparateMonitors=true; restored with owner config after exit' | Set-Content (Join-Path $Result 'span-override.txt')
    }
    if (!$Record) { $sourceReview | Set-Content -LiteralPath (Join-Path $Result 'source-review.json') -Encoding utf8 }
    foreach ($file in @((Join-Path $GameDir 'BepInEx/LogOutput.log'),(Join-Path $data 'Player.log'))) {
        if (Test-Path -LiteralPath $file) { Copy-Exact $file (Join-Path $Result ('previous-'+[IO.Path]::GetFileName($file))) }
    }
    if ($AdmittedPackage -and (Get-FileHash -LiteralPath (Join-Path $Recording 'complete.tsv')).Hash.ToLowerInvariant() -ne $admittedSeal) {
        throw 'The admitted stage seal changed before replay.'
    }
    if ($AdmittedPackage -and (Get-FileHash -LiteralPath (Join-Path $AdmittedPackage 'complete.tsv')).Hash.ToLowerInvariant() -ne $verifiedPackageSeal) {
        throw 'The admitted outer package seal changed before replay.'
    }
    if ($Record) { & $command -Game woden -Action Record -Path $Recording -Seconds $Seconds | Out-Null }
    else { & $command -Game woden -Action Replay -Path $Recording -Output (Join-Path $Result 'playback') | Out-Null }
    $preparedPath=Join-Path $control 'request.txt'
    $id = ((Get-Content -LiteralPath $preparedPath) | Where-Object { $_ -like 'id=*' }).Substring(3)
    if($RequestId){
        Assert-Closed
        Assert-StageRigLease -Path $slot -Token $rigLease.token
        $preparedLines=@(Set-PreparedReplayIdentity ([IO.File]::ReadAllLines($preparedPath)) $RequestId)
        $identityTemp=Join-Path $control ($RequestId+'.identity.tmp')
        if(Test-Path -LiteralPath $identityTemp){throw 'Prepared identity temporary file already exists.'}
        try{[IO.File]::WriteAllLines($identityTemp,[string[]]$preparedLines,[Text.UTF8Encoding]::new($false));[IO.File]::Move($identityTemp,$preparedPath,$true);$id=$RequestId}
        finally{if(Test-Path -LiteralPath $identityTemp){Remove-Item -LiteralPath $identityTemp}}
    }
    Add-Content -LiteralPath (Join-Path $control 'request.txt') -Value 'autoExit=true'
    if (!$Record) { Add-Content -LiteralPath (Join-Path $control 'request.txt') -Value 'coldStart=native-arcade-v1' }
    if ($AdmittedPackage) { $outputsMuted = $true } # Stage-Session replay is muted by the game adapter.
    $request = Get-Content -LiteralPath (Join-Path $control 'request.txt')
    $request | Set-Content -LiteralPath (Join-Path $Result 'request.txt')
    $id = ($request | Where-Object { $_ -like 'id=*' }).Substring(3)
    if($RequestId -and $id -cne $RequestId){throw 'Prepared replay ID differs from requested identity.'}
    Write-Output ((@{ $true='RECORDING: select a single-player Arcade practice stage and drive normally. The game will save and close; no automatic switch to playback.'; $false='PLAYBACK: automatic offline startup.' })[[bool]$Record] + ' Request '+$id)
    Start-Process 'steam://rungameid/3218630'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $seen = $false; $last = ''; $passed = $false
    while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        Start-Sleep -Seconds 1
        if ($borrowedLease) { Assert-StageRigLease -Path $slot -Token $RigLeaseToken }
        $running = Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue
        if ($running) {
            $seen = $true
            if (!$gameProcess) {
                if (@($running).Count -ne 1) { throw 'Multiple Woden processes appeared during replay.' }
                $gameProcess = @($running)[0] # Retain this handle for the numeric exit code after the process closes.
                $gameHandle = $gameProcess.Handle
                if ($gameHandle -eq [IntPtr]::Zero) { throw 'The exact game process handle was unavailable while alive.' }
            } elseif (@($running | Where-Object Id -NE $gameProcess.Id).Count) { throw 'Woden process identity changed during replay.' }
        }
        $statusPath = Join-Path $control 'status.txt'
        if (Test-Path -LiteralPath $statusPath) {
            # The adapter replaces its status while physics is running. A brief
            # sharing violation or replacement gap must not terminate the owner
            # restoration supervisor.
            $status = @(Read-Status $statusPath)
            if ($status -contains ('id='+$id)) {
                $status | Set-Content -LiteralPath (Join-Path $Result 'status.txt')
                $message = ($status | Where-Object { $_ -like 'status=*' })
                if ($message -ne $last) { Write-Output $message; $last=$message }
                $passed = if ($Record) { [bool]($message -like 'status=recorded:*') } else { [bool]($message -like 'status=passed:*') }
            }
        }
        if ($seen -and !$running) { break }
    }
    if (Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue) {
        if (!(Test-Path -LiteralPath (Join-Path $control 'request.txt'))) { & $command -Game woden -Action Stop | Write-Output }
        throw 'Replay deadline exceeded. Stop requested; close the game, then use -RestoreOnly with this Result.'
    }
    if ($gameProcess) {
        $exitEvidence = Get-ObservedGameExitEvidence -Process $gameProcess -ObservedHandle $gameHandle
        $exitEvidence | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $Result 'game-exit.json') -Encoding utf8
    }
    foreach ($file in @((Join-Path $GameDir 'BepInEx/LogOutput.log'),(Join-Path $data 'Player.log'))) {
        if (Test-Path -LiteralPath $file) { Copy-Exact $file (Join-Path $Result ([IO.Path]::GetFileName($file))) }
    }
    if ($borrowedLease) { Assert-StageRigLease -Path $slot -Token $RigLeaseToken }
    Restore-Owner
    if ($Record -and !(Test-Path -LiteralPath (Join-Path $Recording 'complete.tsv'))) { $passed = $false }
    if (!$seen -or !$passed) { throw 'Session did not complete; evidence saved and owner state restored.' }
    if (!$exitEvidence -or !$exitEvidence.shutdownPass) { throw 'Game exit was not observed with numeric code 0.' }
    if ($AdmittedPackage) {
        # The same admitted review after the run: the package and its pinned stage are unchanged.
        $after = Review-AdmittedPackage $AdmittedPackage
        $afterPins = $after | ConvertFrom-Json
        if (([string]$afterPins.stageSealSha256).ToLowerInvariant() -ne $admittedSeal -or
            ([string]$afterPins.sessionSha256).ToLowerInvariant() -ne $admittedSessionSha -or
            ([string]$afterPins.packageSealSha256).ToLowerInvariant() -ne $verifiedPackageSeal) {
            throw 'The admitted package or stage changed during replay.'
        }
        $after | Set-Content -LiteralPath (Join-Path $Result 'source-review.json') -Encoding utf8
    } else { Review-Recording $Recording | Set-Content -LiteralPath (Join-Path $Result 'source-review.json') -Encoding utf8 }
    $sessionSucceeded = $true
} catch {
    $runFailure = $_.Exception.Message
    throw
} finally {
    $cleanupProblem = $null
    try {
        # A failed launch must not leave this recording/replay armed for ordinary play.
        if ($id -and !(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)) {
            $pending = Join-Path $control 'request.txt'
            if ((Test-Path -LiteralPath $pending) -and ((Get-Content -LiteralPath $pending) -contains ('id='+$id))) { Remove-Item -LiteralPath $pending }
        }
        if (!$RestoreOnly -and $rigLease -and (Test-Path -LiteralPath (Join-Path $backup 'manifest.json')) -and
            !(Test-Path -LiteralPath (Join-Path $Result 'restored.txt')) -and !(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)) {
            if ($borrowedLease) { Assert-StageRigLease -Path $slot -Token $RigLeaseToken }
            Restore-Owner
        }
        if ($held) { $lease.ReleaseMutex() }
        $lease.Dispose()
        if ($rigLease -and !$borrowedLease -and !(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)) { $null = Exit-StageRigLease $rigLease }
    } catch {
        $cleanupProblem = $_.Exception.Message
        $sessionSucceeded = $false
        if (!$runFailure) { $runFailure = $cleanupProblem }
    } finally {
        if ($gameProcess) { $gameProcess.Dispose() }
        try { Write-TerminalResult } catch { if (!$cleanupProblem) { $cleanupProblem = $_.Exception.Message } }
    }
    if ($cleanupProblem) { throw $cleanupProblem }
}
if ($sessionSucceeded) { Write-Output ('PASS: requested session completed, game exited with code 0 and owner state restored. '+$Result) }
