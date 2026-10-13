# Read-only postflight for the automatic trials. Restoration is necessary but
# cannot turn an incomplete input trial into a passing runner exit.
function Assert-ControlTrialResult([string]$Result, [string]$Mode) {
    if ($Mode -notin @('shim-menu-trial','shim-car-trial','shim-applied-car-trial','shim-force-capture')) { return }
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
    $expectedReason = if ($Mode -eq 'shim-menu-trial') { 'bounded Core menu trial completed' } else { 'bounded original car trial completed' }
    if ($trial.physicalOutput -isnot [bool] -or $trial.physicalOutput -ne $false -or
        $trial.reason -cne $expectedReason -or $trial.inputVerdict -isnot [string] -or
        !$trial.inputVerdict.StartsWith('passed:', [StringComparison]::Ordinal) -or $trial.rows -le 0) {
        throw "Input trial did not pass: $($trial.reason); $($trial.inputVerdict)"
    }
    if ($Mode -ne 'shim-menu-trial' -and ($trial.carEvidence.kind -cne 'new-original-not-reference-replay' -or
        $trial.carEvidence.nativePlateaus -le 0)) { throw 'No native car plateaus were qualified.' }
    [pscustomobject]@{ schema = 1; mode = $Mode; status = 'passed'; inputVerdict = $trial.inputVerdict
        nativePlateaus = $(if ($Mode -eq 'shim-menu-trial') { $null } else { $trial.carEvidence.nativePlateaus })
        scope = 'Bounded native input consumption and restored configuration; not physical force or normalization' }
}
