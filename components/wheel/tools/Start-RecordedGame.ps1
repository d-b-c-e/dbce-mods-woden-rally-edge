param(
    [Alias('GameDir')][string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge',
    [string]$CaseId,
    [switch]$AttendedFfb,
    [ValidateSet('human','automated')][string]$InputOrigin,
    [switch]$PrepareOnly,
    [switch]$ExternalLaunch,
    [string]$NativeObservationSource,
    [ValidateRange(5,15)][int]$NativeObservationSeconds = 10,
    [switch]$Stop,
    [guid]$RequestId = [guid]::Empty,
    [ValidateRange(15,600)][int]$StartupTimeoutSeconds = 180,
    [ValidateRange(1,30)][int]$SessionTimeoutMinutes = 25
)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
$exe = Join-Path $gameRoot 'Super Woden Rally Edge.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Woden executable not found.' }
function Read-ControlStatus([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
    try {
        if ($stream.Length -gt 8192) { throw 'Malformed recording status.' }
        $bytes = [byte[]]::new([int]$stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $read = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($read -le 0) { throw 'Malformed recording status.' }
            $offset += $read
        }
        if ($stream.ReadByte() -ne -1) { throw 'Malformed recording status.' }
    } finally { $stream.Dispose() }
    try { $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes) }
    catch [Text.DecoderFallbackException] { throw 'Malformed recording status.' }
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
    $fields = @{}
    $lines = @($text -split "`n")
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.EndsWith("`r")) { $line = $line.Substring(0, $line.Length - 1) }
        if ($line.Length -eq 0 -and $i -eq $lines.Count - 1) { continue }
        $equal = $line.IndexOf('=')
        if ($equal -le 0 -or $fields.ContainsKey($line.Substring(0, $equal))) { throw 'Malformed recording status.' }
        $fields[$line.Substring(0, $equal)] = $line.Substring($equal + 1)
    }
    if ($fields.Count -eq 0) { throw 'Malformed recording status.' }
    return $fields
}
function Write-Finalization([string]$ControlRoot, [guid]$Id, [string]$State, [string]$ErrorText) {
    $path = Join-Path $ControlRoot 'finalization.json'
    if (Test-Path -LiteralPath $path) { throw "Finalization notice already exists: $path" }
    $temp = "$path.tmp-$([guid]::NewGuid().ToString('N'))"
    try {
        [IO.File]::WriteAllText($temp, ([ordered]@{ Version = 1; RequestId = $Id; State = $State; Error = $ErrorText } | ConvertTo-Json -Compress), [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temp, $path)
    } finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp } }
}
if ($Stop) {
    if ($RequestId -eq [guid]::Empty -or $PrepareOnly -or $ExternalLaunch -or $AttendedFfb -or $CaseId -or $NativeObservationSource -or $PSBoundParameters.ContainsKey('InputOrigin')) { throw 'Stop requires only an exact RequestId.' }
    $captureDirectory = Join-Path $gameRoot ('BepInEx/WodenRecordings/request-' + $RequestId.ToString('N'))
    $controlRoot = Join-Path $captureDirectory 'control'
    $status = Read-ControlStatus (Join-Path $controlRoot 'status.txt')
    if ($null -eq $status -or $status.id -ne $RequestId.ToString('N') -or $status.status -ne 'Recording' -or $status.active -ne 'True') { throw 'The exact ordinary recorder is not active.' }
    $processId = 0; $startedTicks = [long]0
    if (![int]::TryParse($status.pid, [ref]$processId) -or ![long]::TryParse($status.startedUtcTicks, [ref]$startedTicks)) { throw 'Recording process identity is invalid.' }
    $ownerProcess = Get-Process -Id $processId -ErrorAction Stop
    if ($ownerProcess.ProcessName -ne 'Super Woden Rally Edge' -or $ownerProcess.StartTime.ToUniversalTime().Ticks -ne $startedTicks -or
        ![string]::Equals($ownerProcess.Path, $exe, [StringComparison]::OrdinalIgnoreCase)) { throw 'Recording process identity changed; no Stop sent.' }
    & (Join-Path $PSScriptRoot 'RecordedStopControl.ps1') -RequestId $RequestId -ControlRoot $controlRoot -ExpiresUtc ([DateTimeOffset]::UtcNow.AddMinutes(2))
    Write-Host "Stop requested for exact ordinary recording $RequestId. Woden remains open; wait for source/case validation."
    return
}
if ($ExternalLaunch -and ($PrepareOnly -or $AttendedFfb -or !$NativeObservationSource -or $InputOrigin -cne 'automated')) { throw 'ExternalLaunch requires the explicit automated no-force Native observation companion.' }
if (Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue) { throw 'Close Woden normally before preparing a recorded launch.' }
if ((Get-FileHash -LiteralPath (Join-Path $gameRoot 'GameAssembly.dll')).Hash -ne 'F422894D8D2B0DF4EDB7E5259E5E60CB8C4F8DEA2E85EBDFC09DD6766349250C') { throw 'Unsupported game build.' }
$plugin = Join-Path $gameRoot 'BepInEx/plugins/WodenRallyEdgeWheel/WodenRallyEdgeWheel.dll'
if (!(Test-Path -LiteralPath $plugin) -or [version][Diagnostics.FileVersionInfo]::GetVersionInfo($plugin).FileVersion -lt [version]'0.2.12') { throw 'Install Woden Wheel 0.2.12 or later first.' }
if ($RequestId -eq [guid]::Empty) { $RequestId = [guid]::NewGuid() }
if (!$CaseId) { $CaseId = 'woden-drive-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') }
if ($CaseId -notmatch '^[A-Za-z0-9][A-Za-z0-9_.:@-]{0,127}$') { throw 'CaseId must follow the shared identifier form: 1-128 ASCII letters, digits, dot, underscore, colon, at-sign or hyphen, beginning with a letter or digit.' }
if ($PSBoundParameters.ContainsKey('InputOrigin') -and $InputOrigin -cnotin @('human','automated')) { throw 'InputOrigin must be exactly human or automated.' }
if ($InputOrigin -ceq 'automated' -and $AttendedFfb) { throw 'Automated input cannot request physical FFB.' }
$requestPath = Join-Path $gameRoot 'BepInEx/config/woden-record-next-launch.json'
if (Test-Path -LiteralPath $requestPath) { throw "A launch request already exists: $requestPath. Inspect it before replacing it." }
$captureDirectory = Join-Path $gameRoot ('BepInEx/WodenRecordings/request-' + $requestId.ToString('N'))
if (Test-Path -LiteralPath $captureDirectory) { throw "Recording request directory already exists: $captureDirectory" }
$request = [ordered]@{ Version = 2; Id = $requestId.ToString(); ExpiresUtc = [DateTimeOffset]::UtcNow.AddMinutes(15).ToString('o'); DisableForces = !$AttendedFfb; CaseId = $CaseId }
if ($PSBoundParameters.ContainsKey('InputOrigin')) { $request.InputOrigin = $InputOrigin }
if ($NativeObservationSource) {
    if ($AttendedFfb -or $InputOrigin -cne 'automated') { throw 'Native observation requires automated origin and suppressed force.' }
    $reference = (Resolve-Path -LiteralPath $NativeObservationSource -ErrorAction Stop).Path
    if (!(Test-Path -LiteralPath (Join-Path $reference 'stage-context.json') -PathType Leaf)) { throw 'Native observation requires the sealed stage reference.' }
    $request.NativeObservationSource = $reference
    $request.NativeObservationSeconds = $NativeObservationSeconds
    $request.ExpiresUtc = [DateTimeOffset]::UtcNow.AddMinutes(5).ToString('o')
}
$request | ConvertTo-Json | Set-Content -LiteralPath $requestPath -Encoding utf8
Write-Host "Capture requested for one launch: $($request.Id). Expires in 15 minutes."
Write-Host "Case: $CaseId (signal reprocessing; not deterministic game-driving replay)."
Write-Host "Physical FFB allowed for this diagnostic launch: $([bool]$AttendedFfb). Saved preferences are preserved."
Write-Host "Input origin: $(if ($PSBoundParameters.ContainsKey('InputOrigin')) { $InputOrigin } else { 'not stated' })."
if (!$AttendedFfb) { Write-Host 'No-force mode validates capture channels but resets force shaping at each blocked sample; use an explicitly attended FFB drive for tuning trials.' }
$controlRoot = Join-Path $captureDirectory 'control'
Write-Host "Expected capture directory: $captureDirectory"
if ($PrepareOnly) { Write-Host 'PrepareOnly: no game launched, input injected, device opened or force actuated.'; return }

if ($ExternalLaunch) { Write-Host 'ExternalLaunch: the lease-owning supervisor supplies the exact shared Steam launch; companion owns request/capture/finalization only.' }
else { Start-Process -FilePath 'steam://rungameid/3218630' -WindowStyle Hidden | Out-Null }
$startupDeadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
do {
    $running = @(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { break }
    Start-Sleep -Milliseconds 250
} while ([DateTimeOffset]::UtcNow -lt $startupDeadline)
if ($running.Count -eq 0) { throw "Woden did not appear within $StartupTimeoutSeconds seconds. No process was stopped; the expiring request remains at $requestPath." }
Write-Host "Woden started. Use F6 Telemetry Stop, the exact -Stop -RequestId command, or normal exit to end capture (20 minutes / 1.75 GiB maximum)."
$sessionDeadline = [DateTimeOffset]::UtcNow.AddMinutes($SessionTimeoutMinutes)
do {
    Start-Sleep -Milliseconds 500
    $running = @(Get-Process -Name 'Super Woden Rally Edge' -ErrorAction SilentlyContinue)
    $status = Read-ControlStatus (Join-Path $controlRoot 'status.txt')
    if ($null -ne $status -and $status.id -eq $requestId.ToString('N') -and $status.status -in @('Completed','Faulted','LimitReached')) { break }
} while ($running.Count -gt 0 -and [DateTimeOffset]::UtcNow -lt $sessionDeadline)
if ($running.Count -gt 0 -and ($null -eq $status -or $status.status -notin @('Completed','Faulted','LimitReached'))) { throw "Woden is still recording after $SessionTimeoutMinutes minutes. No process was stopped; expected capture remains $captureDirectory." }

$source = Join-Path $captureDirectory 'source.jsonl'
$finalizeDeadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
while (!(Test-Path -LiteralPath $source) -and [DateTimeOffset]::UtcNow -lt $finalizeDeadline) { Start-Sleep -Milliseconds 250 }
if (!(Test-Path -LiteralPath $source)) { throw "The correlated source recording was not found: $source" }
$finalizer = Join-Path $PSScriptRoot 'WodenCaseFinalizer.exe'
if (!(Test-Path -LiteralPath $finalizer -PathType Leaf)) { throw "Installed case finalizer is missing: $finalizer" }
& $finalizer reprocess $captureDirectory
$reprocessed = $LASTEXITCODE -eq 0
# A first-stage package the game handed off at close (or a commit interrupted when its writer ended: a descriptor
# without its outer seal) is completed before any finalization notice, whether or not the force case qualified. The two
# outcomes stay separate: the notice's State is the force case only, and the package's own files (session.json +
# complete.tsv, or full-session-incomplete.txt) say how the package ended. The finalizer refuses, writing nothing,
# while a live writer holds the package lock.
$packageNote = $null
$inCapture = { param($name) Test-Path -LiteralPath (Join-Path $captureDirectory $name) -PathType Leaf }
if ((& $inCapture 'full-session-pending.json') -or ((& $inCapture 'session.json') -and !(& $inCapture 'complete.tsv') -and !(& $inCapture 'full-session-incomplete.txt'))) {
    $packageOutput = & $finalizer package $captureDirectory
    try { $packageResult = ($packageOutput | Out-String) | ConvertFrom-Json } catch { $packageResult = $null }
    $packageNote = switch ($packageResult.Outcome) {
        'package' { 'First-stage package sealed by the companion.' }
        'incomplete' { "First-stage package not sealed: $($packageResult.Reason) (full-session-incomplete.txt)." }
        'refused' { "First-stage package step refused, nothing written: $($packageResult.Reason)." }
        default { 'First-stage package step gave no readable result; the capture files are kept.' }
    }
}
if (!$reprocessed) {
    Write-Finalization $controlRoot $requestId 'incomplete' 'Source or driving signal validation failed; original source retained.'
    if ($packageNote) { Write-Host $packageNote }
    throw "Recording source closed but driving readiness/signal reprocessing failed. Source retained unchanged: $source"
}
Write-Finalization $controlRoot $requestId 'saved' ''
if ($packageNote) { Write-Host $packageNote }
$observation = Join-Path $captureDirectory 'force-observation.jsonl'
Write-Host "Source: $source"
Write-Host "Source SHA-256: $((Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant())"
Write-Host "Observation: $observation"
Write-Host "Observation SHA-256: $((Get-FileHash -LiteralPath $observation).Hash.ToLowerInvariant())"
Write-Host "Case manifest: $(Join-Path $captureDirectory 'case.json')"
