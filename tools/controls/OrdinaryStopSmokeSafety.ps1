# Small gates shared by the supervisor and its fake-process refusal checks.
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
