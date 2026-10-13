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
$rigScript = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../vendor/playback/Stage-RigLease.ps1'))
$rigPin = Get-Content (Join-Path (Split-Path $rigScript -Parent) 'rig-lease-provenance.json') -Raw | ConvertFrom-Json
if ($rigPin.schema -ne 'dbce.stage-rig-lease-pin@1' -or (Get-FileHash -LiteralPath $rigScript).Hash -ine $rigPin.sha256) { throw 'Rig lease source pin differs' }
. $rigScript
. (Join-Path $PSScriptRoot 'ReplayEnvironment.ps1')
# PowerShell's location can differ from the process CWD (especially Start-Job).
# Resolve caller paths here before passing them to .NET or the native inspector.
$Result = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Result)
$GameDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GameDir)
if ($Recording) { $Recording = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Recording) }
if ($AdmittedPackage) { $AdmittedPackage = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($AdmittedPackage) }
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
function Review-Recording([string]$Path) {
    $inspector = Join-Path $PSScriptRoot '../../components/wheel/tools/TelemetryInspector/bin/Release/net10.0/TelemetryInspector.dll'
    if (!(Test-Path -LiteralPath $inspector)) { throw 'Build TelemetryInspector before running a stage session.' }
    $report = & dotnet $inspector stage-review $Path
    if ($LASTEXITCODE -ne 0) { throw 'Original recording validation failed.' }
    return ($report -join [Environment]::NewLine)
}
function Review-AdmittedPackage([string]$Path) {
    $inspector = Join-Path $PSScriptRoot '../../components/wheel/tools/TelemetryInspector/bin/Release/net10.0/TelemetryInspector.dll'
    if (!(Test-Path -LiteralPath $inspector)) { throw 'Build TelemetryInspector before running a stage session.' }
    $report = & dotnet $inspector ordinary-stage-review $Path
    if ($LASTEXITCODE -ne 0) { throw 'Admitted ordinary package validation failed.' }
    return ($report -join [Environment]::NewLine)
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
Assert-Closed
$lease = [Threading.Mutex]::new($false,'Local\DbceStageReplayRunner')
$held = $false
try {
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
    if ($Record) { & $command -Game woden -Action Record -Path $Recording -Seconds $Seconds | Out-Null }
    else { & $command -Game woden -Action Replay -Path $Recording -Output (Join-Path $Result 'playback') | Out-Null }
    Add-Content -LiteralPath (Join-Path $control 'request.txt') -Value 'autoExit=true'
    if (!$Record) { Add-Content -LiteralPath (Join-Path $control 'request.txt') -Value 'coldStart=native-arcade-v1' }
    $request = Get-Content -LiteralPath (Join-Path $control 'request.txt')
    $request | Set-Content -LiteralPath (Join-Path $Result 'request.txt')
    $id = ($request | Where-Object { $_ -like 'id=*' }).Substring(3)
    Write-Output ((@{ $true='RECORDING: select a single-player Arcade practice stage and drive normally. The game will save and close; no automatic switch to playback.'; $false='PLAYBACK: automatic offline startup.' })[[bool]$Record] + ' Request '+$id)
    Start-Process 'steam://rungameid/3218630'
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $seen = $false; $last = ''; $passed = $false
    while ($clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        Start-Sleep -Seconds 1
        if ($borrowedLease) { Assert-StageRigLease -Path $slot -Token $RigLeaseToken }
        $running = Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue
        if ($running) { $seen = $true }
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
    foreach ($file in @((Join-Path $GameDir 'BepInEx/LogOutput.log'),(Join-Path $data 'Player.log'))) {
        if (Test-Path -LiteralPath $file) { Copy-Exact $file (Join-Path $Result ([IO.Path]::GetFileName($file))) }
    }
    if ($borrowedLease) { Assert-StageRigLease -Path $slot -Token $RigLeaseToken }
    Restore-Owner
    if ($Record -and !(Test-Path -LiteralPath (Join-Path $Recording 'complete.tsv'))) { $passed = $false }
    if (!$seen -or !$passed) { throw 'Session did not complete; evidence saved and owner state restored.' }
    if ($AdmittedPackage) {
        # The same admitted review after the run: the package and its pinned stage are unchanged.
        $after = Review-AdmittedPackage $AdmittedPackage
        if (([string]($after | ConvertFrom-Json).stageSealSha256).ToLowerInvariant() -ne $admittedSeal) { throw 'The admitted stage seal changed during replay.' }
        $after | Set-Content -LiteralPath (Join-Path $Result 'source-review.json') -Encoding utf8
    } else { Review-Recording $Recording | Set-Content -LiteralPath (Join-Path $Result 'source-review.json') -Encoding utf8 }
    Write-Output ('PASS: requested session completed, game closed and owner state restored. '+$Result)
} finally {
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
}
