# Read-only postflight for the automatic trials. Restoration is necessary but
# cannot turn an incomplete input trial into a passing runner exit.
function Assert-ControlTrialResult([string]$Result, [string]$Mode) {
    if ($Mode -notin @('shim-menu-trial','shim-car-trial','shim-applied-car-trial','shim-applied-forward-capture','shim-force-capture','shim-hud-ordinary','shim-hud-stop-only')) { return }
    function Read-TrialJson([string]$Name) {
        $path = Join-Path $Result $Name
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing trial evidence: $Name" }
        if ((Get-Item -LiteralPath $path).Length -gt 65536) { throw "Trial evidence exceeds limit: $Name" }
        Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -ErrorAction Stop
    }
    if (!(Test-Path -LiteralPath (Join-Path $Result 'restored.txt') -PathType Leaf)) { throw 'Owner restoration is not complete.' }
    $runtime = Read-TrialJson 'runtime-verification.json'
    if ($runtime.status -cne 'passed' -or $runtime.mode -cne $Mode) { throw 'Runtime configuration verification did not pass for this trial.' }
    $trial = Read-TrialJson 'trace/result.json'
    if($Mode -eq 'shim-hud-stop-only'){
        if($trial.physicalOutput -isnot [bool] -or $trial.physicalOutput -ne $false -or
            $trial.reason -cne 'bounded menu recorder stop and dwell completed' -or
            $trial.inputVerdict -notmatch '^passed: 2s menu Recording, probe-direct Stop, 5s Completed/DataWritten dwell;' -or
            $trial.rows -le 0 -or $trial.carEvidence){throw "HUD menu stop trial did not pass: $($trial.reason); $($trial.inputVerdict)"}
        $finalizer=Read-TrialJson 'ordinary-finalizer.json'
        $capture=Read-TrialJson 'hud-capture-verdict.json'
        $post=Read-TrialJson 'hud-poststop/capture.json'
        $context=Read-TrialJson 'context.json'
        if($finalizer.stop -cne 'probe-direct-stop' -or
            $finalizer.sourcePins.'source.jsonl' -notmatch '^[A-Fa-f0-9]{64}$' -or
            $finalizer.sourcePins.'force-config.json' -notmatch '^[A-Fa-f0-9]{64}$' -or
            $finalizer.sourcePins.'capture-profile.json' -notmatch '^[A-Fa-f0-9]{64}$' -or
            $finalizer.status -notin @('passed','refused-no-driving') -or
            ($finalizer.status -eq 'refused-no-driving' -and ($finalizer.reprocessExit -eq 0 -or $finalizer.reason -notmatch 'Insufficient driving coverage: 0 samples')) -or
            ($finalizer.status -eq 'passed' -and ($finalizer.caseSha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $finalizer.observationSha256 -notmatch '^[A-Fa-f0-9]{64}$'))){
            throw 'Archived menu source/finalizer result is incomplete or refused unexpectedly.'
        }
        if($capture.status -cne 'captured' -or $post.status -cne 'complete' -or
            $post.schema -cne 'dbce.window-burst@1' -or $post.saved -lt 2 -or @($post.frames).Count -ne $post.saved){
            throw 'Post-Stop HUD burst is incomplete.'
        }
        if($post.process -ne $context.process -or [DateTimeOffset]$post.processStartedUtc -ne [DateTimeOffset]$context.startedUtc){
            throw 'Post-Stop HUD burst does not belong to the exact trial process.'
        }
        $stopLine=Select-String -LiteralPath (Join-Path $Result 'trace/observations.tsv') -Pattern 'hud-recorder-stop.*utc=' | Select-Object -Last 1 -ExpandProperty Line
        $stopUtc=[regex]::Match([string]$stopLine,'utc=([^\s]+)')
        if(!$stopUtc.Success -or [DateTimeOffset]$post.startedUtc -lt [DateTimeOffset]::Parse($stopUtc.Groups[1].Value)){
            throw 'Post-Stop HUD burst did not start after the probe-direct Stop.'
        }
        return [pscustomobject]@{schema=1;mode=$Mode;status='passed';inputVerdict=$trial.inputVerdict;ordinaryFinalizer=$finalizer.status;
            postStopHudSampleSeconds=$post.frames[-1].ArrivalElapsedSeconds-$post.frames[0].ArrivalElapsedSeconds;
            scope='Menu-only ordinary recorder Stop, five-second DataWritten dwell and sampled post-Stop HUD frames; no injected car input or physical force'}
    }
    $expectedReason = if ($Mode -eq 'shim-menu-trial') { 'bounded Core menu trial completed' } else { 'bounded original car trial completed' }
    if ($trial.physicalOutput -isnot [bool] -or $trial.physicalOutput -ne $false -or
        $trial.reason -cne $expectedReason -or $trial.inputVerdict -isnot [string] -or
        !$trial.inputVerdict.StartsWith('passed:', [StringComparison]::Ordinal) -or $trial.rows -le 0) {
        throw "Input trial did not pass: $($trial.reason); $($trial.inputVerdict)"
    }
    if ($Mode -ne 'shim-menu-trial' -and ($trial.carEvidence.kind -cne 'new-original-not-reference-replay' -or
        $trial.carEvidence.nativePlateaus -le 0)) { throw 'No native car plateaus were qualified.' }
    if ($Mode -eq 'shim-applied-forward-capture' -and ($trial.carEvidence.recipe -cne 'applied-forward-50pct-8s@1' -or
        $trial.carEvidence.nativePlateaus -ne 3 -or $trial.carEvidence.forceCapture.forwardGroundedSeconds -lt 2 -or
        $trial.carEvidence.forceCapture.qualifiedRows -le 0)) { throw 'Applied forward capture lacks measured qualified reference rows.' }
    if($Mode -eq 'shim-hud-ordinary'){
        if($trial.carEvidence.recipe -cne 'forward-50pct-8s@1' -or $trial.inputVerdict -notmatch 'probe-direct-stop, 5s HUD dwell'){
            throw 'HUD ordinary mode did not finish its exact probe stop and dwell.'
        }
        $finalizer=Read-TrialJson 'ordinary-finalizer.json'
        $capture=Read-TrialJson 'hud-capture-verdict.json'
        $burst=Read-TrialJson 'hud-burst/capture.json'
        $post=Read-TrialJson 'hud-poststop/capture.json'
        $context=Read-TrialJson 'context.json'
        if($finalizer.status -cne 'passed' -or $finalizer.stop -cne 'probe-direct-stop' -or
            $finalizer.sourcePins.'source.jsonl' -notmatch '^[A-Fa-f0-9]{64}$' -or
            $finalizer.caseSha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $finalizer.observationSha256 -notmatch '^[A-Fa-f0-9]{64}$'){
            throw 'Archived ordinary source did not finalize as a pinned case and observation.'
        }
        if($capture.status -cne 'captured' -or $burst.status -cne 'complete' -or
            $burst.schema -cne 'dbce.window-burst@1' -or $burst.saved -lt 3 -or @($burst.frames).Count -ne $burst.saved -or
            $post.status -cne 'complete' -or $post.schema -cne 'dbce.window-burst@1' -or $post.saved -lt 2 -or @($post.frames).Count -ne $post.saved){
            throw 'Same-run moving and post-Stop HUD bursts are incomplete.'
        }
        if($burst.process -ne $context.process -or $post.process -ne $context.process -or
            [DateTimeOffset]$burst.processStartedUtc -ne [DateTimeOffset]$context.startedUtc -or
            [DateTimeOffset]$post.processStartedUtc -ne [DateTimeOffset]$context.startedUtc){throw 'HUD bursts do not belong to the exact trial process.'}
        $stopLine=Select-String -LiteralPath (Join-Path $Result 'trace/observations.tsv') -Pattern 'car-recorder-stop.*utc=' | Select-Object -Last 1 -ExpandProperty Line
        $stopUtc=[regex]::Match([string]$stopLine,'utc=([^\s]+)')
        if(!$stopUtc.Success -or [DateTimeOffset]$post.startedUtc -lt [DateTimeOffset]::Parse($stopUtc.Groups[1].Value)){
            throw 'Post-Stop HUD burst did not start after the probe-direct Stop.'
        }
    }
    [pscustomobject]@{ schema = 1; mode = $Mode; status = 'passed'; inputVerdict = $trial.inputVerdict
        nativePlateaus = $(if ($Mode -eq 'shim-menu-trial') { $null } else { $trial.carEvidence.nativePlateaus })
        movingHudSampleSeconds = $(if($Mode -eq 'shim-hud-ordinary'){$burst.frames[-1].ArrivalElapsedSeconds-$burst.frames[0].ArrivalElapsedSeconds}else{$null})
        postStopHudSampleSeconds = $(if($Mode -eq 'shim-hud-ordinary'){$post.frames[-1].ArrivalElapsedSeconds-$post.frames[0].ArrivalElapsedSeconds}else{$null})
        scope = $(if($Mode -eq 'shim-hud-ordinary'){'Bounded native input, probe-direct ordinary case finalization and sampled HUD frames; no physical force or jitter verdict'}else{'Bounded native input consumption and restored configuration; not physical force or normalization'}) }
}
