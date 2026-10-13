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
function Assert-OrdinaryGameExit($GameProcess) {
    if (!$GameProcess.HasExited) { [void]$GameProcess.CloseMainWindow() }
    if (!$GameProcess.WaitForExit(30000)) { throw 'Exact Woden process did not exit normally; retain lease and owner state.' }
    if ($GameProcess.ExitCode -ne 0) { throw "Exact Woden process exited with $($GameProcess.ExitCode), not a normal exit." }
}
