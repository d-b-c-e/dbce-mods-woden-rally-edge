# Supervised real-drive recording and cold-launch offline replay. The adapter owns menus/physics; this runner owns
# the process deadline, evidence and exact local owner-state restoration.
[CmdletBinding()]
param(
    [switch]$Record,
    [ValidateRange(10,1800)][int]$Seconds = 60,
    [string]$Recording,
    [Parameter(Mandatory)][string]$Result,
    [string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge',
    [ValidateRange(240,2400)][int]$TimeoutSeconds = 360,
    [switch]$RestoreOnly,
    [switch]$CheckEnvironment
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReplayEnvironment.ps1')
$Result = [IO.Path]::GetFullPath($Result)
$GameDir = [IO.Path]::GetFullPath($GameDir)
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
    if ($RestoreOnly) { Restore-Owner; Write-Output 'Owner files and preferences restored and verified.'; return }
    if (Test-Path -LiteralPath $Result) { throw 'Choose a new result directory.' }
    if ($CheckEnvironment) { Save-Owner; Restore-Owner; Write-Output 'PASS: owner environment backup and exact readback.'; return }
    if ($Record -and $Recording) { throw 'Choose either Record or a completed Recording.' }
    if (!$Record -and (!$Recording -or !(Test-Path -LiteralPath (Join-Path $Recording 'complete.tsv')))) { throw 'A completed recording is required.' }
    if (!$Record) { $sourceReview = Review-Recording $Recording }
    if ($Record) { $Recording = Join-Path $Result 'recording' }
    if (Test-Path -LiteralPath (Join-Path $control 'request.txt')) { throw 'An unconsumed stage request exists.' }
    $other = Get-Process -Name 'artofrally','iracing-arcade','DRIVERally' -ErrorAction SilentlyContinue
    if ($other) { throw 'Another game owns the test slot.' }
    $command = Join-Path $GameDir 'BepInEx/plugins/WodenRallyEdgeWheel/Stage-Session.ps1'
    Save-Owner
    if (!$Record) { $sourceReview | Set-Content -LiteralPath (Join-Path $Result 'source-review.json') -Encoding utf8 }
    foreach ($file in @((Join-Path $GameDir 'BepInEx/LogOutput.log'),(Join-Path $data 'Player.log'))) {
        if (Test-Path -LiteralPath $file) { Copy-Exact $file (Join-Path $Result ('previous-'+[IO.Path]::GetFileName($file))) }
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
    Restore-Owner
    if ($Record -and !(Test-Path -LiteralPath (Join-Path $Recording 'complete.tsv'))) { $passed = $false }
    if (!$seen -or !$passed) { throw 'Session did not complete; evidence saved and owner state restored.' }
    Review-Recording $Recording | Set-Content -LiteralPath (Join-Path $Result 'source-review.json') -Encoding utf8
    Write-Output ('PASS: requested session completed, game closed and owner state restored. '+$Result)
} finally {
    if ((Test-Path -LiteralPath (Join-Path $backup 'manifest.json')) -and !(Test-Path -LiteralPath (Join-Path $Result 'restored.txt')) -and !(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)) {
        Restore-Owner
    }
    if ($held) { $lease.ReleaseMutex() }
    $lease.Dispose()
}
