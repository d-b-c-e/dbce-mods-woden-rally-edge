# Small gates shared by the supervisor and its fake-process refusal checks.
function Set-OrdinaryTelemetryDeliveryOff([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $sections = [regex]::Matches($text, '(?ms)^\[Telemetry\][ \t]*\r?\n.*?(?=^\[|\z)')
    if ($sections.Count -ne 1) { throw 'Expected exactly one Telemetry section.' }
    $section = $sections[0].Value
    $keys = [regex]::Matches($section, '(?m)^[ \t]*Enabled[ \t]*=[ \t]*[^\r\n]*')
    if ($keys.Count -ne 1 -or $keys[0].Value -notmatch '^[ \t]*Enabled[ \t]*=[ \t]*(true|false)[ \t]*$') {
        throw 'Expected exactly one boolean Telemetry/Enabled key.'
    }
    $updated = $section.Substring(0,$keys[0].Index) + 'Enabled = false' + $section.Substring($keys[0].Index + $keys[0].Length)
    [IO.File]::WriteAllText($Path, $text.Substring(0,$sections[0].Index) + $updated + $text.Substring($sections[0].Index + $section.Length))
    $check = [IO.File]::ReadAllText($Path)
    if ($check -cne ($text.Substring(0,$sections[0].Index) + $updated + $text.Substring($sections[0].Index + $section.Length))) {
        throw 'Temporary telemetry delivery mute did not persist.'
    }
}
function Assert-OrdinaryCompanionStopped($Child,[string]$RequestPath) {
    if ($Child -and !$Child.process.WaitForExit(15000)) { throw 'Companion can still launch Woden; retain lease and owner state.' }
    if (Test-Path -LiteralPath $RequestPath) { throw 'One-shot launch request remains; retain lease and owner state.' }
}
function Assert-OrdinaryNoDrivingRefusal([int]$Exit,[string]$Output) {
    if ($Exit -eq 0) { throw 'Menu-only companion unexpectedly produced a driving-ready case.' }
    if ($Output -notmatch 'REPROCESS REFUSED: Insufficient driving coverage: 0 samples') {
        throw 'Installed finalizer did not report the expected no-driving refusal.'
    }
}
function Assert-OrdinarySourceMute($Header,[guid]$RequestId,[string]$ForceConfigPath,[string]$CaptureProfilePath) {
    $properties = $Header.metadata.properties
    if ($Header.kind -cne 'metadata' -or $properties.requestId -ne $RequestId.ToString() -or $properties.disableForces -cne 'true' -or
        $properties.forceConfigSha256 -ne (Get-FileHash -LiteralPath $ForceConfigPath).Hash -or
        $properties.captureProfileSha256 -ne (Get-FileHash -LiteralPath $CaptureProfilePath).Hash) {
        throw 'Original source does not prove this no-force request and capture inputs.'
    }
    # capture-profile.ffbEnabled is the saved owner preference, not the
    # diagnostic request's DisableForces gate or native load-time latch.
}
function Assert-OrdinaryGameExit($GameProcess) {
    if (!$GameProcess.HasExited) { [void]$GameProcess.CloseMainWindow() }
    if (!$GameProcess.WaitForExit(30000)) { throw 'Exact Woden process did not exit normally; retain lease and owner state.' }
    $GameProcess.Refresh() # Get-Process/GetProcessById needs this before ExitCode readback.
    if ($GameProcess.ExitCode -ne 0) { throw "Exact Woden process exited with $($GameProcess.ExitCode), not a normal exit." }
}
function Assert-OrdinaryNativeObservationLog([string]$LogPath) {
    # Called only after Assert-OrdinaryGameExit and Closed: the live game keeps BepInEx/LogOutput.log open for writing,
    # so an earlier read is a sharing violation (actual 08), not something to retry.
    $text = [IO.File]::ReadAllText($LogPath)
    if ($text -notmatch 'Native observation phase=ended; captured-current-reader;' -or $text -match 'Native observation phase=ended; (unavailable|refused):') {
        throw 'Native observation did not complete its first-reader interval.'
    }
}
function Wait-OrdinaryGameExitForRestore($GameProcess) {
    # Caller supplies the exact process admitted by request ID, start ticks and executable. An error may occur
    # after the recorder stops but before its scheduled normal Quit; never restore underneath that process.
    if (!$GameProcess) { return $null }
    if (!$GameProcess.WaitForExit(30000)) { throw 'Exact Woden process still alive after bounded restore wait; retain lease and owner state.' }
    $GameProcess.Refresh()
    # A crash is a failed functional run, but an exited process must not prevent restoring the owner snapshot.
    [ordered]@{exited=$true;exitCode=$GameProcess.ExitCode;closeSent=$false;killSent=$false}
}
