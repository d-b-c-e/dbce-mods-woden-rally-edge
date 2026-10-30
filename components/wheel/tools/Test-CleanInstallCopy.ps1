# Woden clean-install lane, Phase A: file level only, on a disposable mod-free copy. No game launch.
# install (loader from the pinned local archive) -> verify -> uninstall -> reinstall -> rollback to an older package -> candidate again.
# Prepare <Root>\game first: robocopy <game> <Root>\game /E /XD <game>\BepInEx <game>\dotnet <game>\WodenWheelBackups
# /XF winhttp.dll doorstop_config.ini .doorstop_version. Uninstall keeps the shared loader, the backups folder and the
# receipt by design (Manage-Install sharedLoaderRetained; the loader's root files include changelog.txt), so "pristine" means every original game file is unchanged and
# every mod payload is gone; the leftovers are listed. Result: <Root>\phaseA-result.json.
param(
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory)][string]$Candidate,    # extracted package folder (has Manage-Install.ps1 and manifest.json)
    [Parameter(Mandatory)][string]$Previous,     # extracted older package folder for the rollback step
    [Parameter(Mandatory)][string]$LoaderArchive
)
$ErrorActionPreference = 'Stop'
$game = Join-Path $Root 'game'
$results = [ordered]@{}
$failures = [System.Collections.Generic.List[string]]::new()
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $results[$name] = [ordered]@{ ok = $ok; detail = $detail }
    if (-not $ok) { $failures.Add("$name $detail") }
    Write-Host ("{0} {1} {2}" -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $detail)
}
function Snapshot {
    $map = @{}
    Get-ChildItem -LiteralPath $game -Recurse -File | ForEach-Object {
        $map[$_.FullName.Substring($game.Length + 1)] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
    $map
}
function Compare-Snapshot($a, $b) {
    [ordered]@{
        added = @($b.Keys | Where-Object { -not $a.ContainsKey($_) })
        removed = @($a.Keys | Where-Object { -not $b.ContainsKey($_) })
        changed = @($a.Keys | Where-Object { $b.ContainsKey($_) -and $a[$_] -ne $b[$_] })
    }
}
function Run([string]$pkg, [string]$mode = 'Install') {
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $pkg 'Manage-Install.ps1') -Mode $mode -GameDir $game -LoaderArchive $LoaderArchive 2>&1 | Out-String
    [ordered]@{ exit = $LASTEXITCODE; output = $out }
}
function Payloads([string]$pkg) { @(Get-Content -Raw -LiteralPath (Join-Path $pkg 'manifest.json') | ConvertFrom-Json | Where-Object { $_.path -like 'BepInEx*' }) }
function PayloadMatches([string]$pkg) {
    $bad = @(Payloads $pkg | Where-Object {
        $installed = Join-Path $game $_.path
        -not (Test-Path -LiteralPath $installed) -or (Get-FileHash -LiteralPath $installed).Hash -ne $_.sha256.ToUpperInvariant() })
    $bad.Count -eq 0
}

if (Get-Process | Where-Object { $_.ProcessName -match 'Woden' }) { throw 'Super Woden Rally Edge is running; Phase A never runs beside the game.' }
if (-not (Test-Path (Join-Path $game 'GameAssembly.dll'))) { throw "No game copy at $game" }
$pristine = Snapshot
Check 'pristine-has-no-loader' (-not (Test-Path (Join-Path $game 'winhttp.dll')) -and -not (Test-Path (Join-Path $game 'BepInEx'))) "$($pristine.Count) files"

$r = Run $Candidate
Check 'install-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'install-placed-loader' ((Test-Path (Join-Path $game 'winhttp.dll')) -and (Test-Path (Join-Path $game 'BepInEx\core\BepInEx.Core.dll')))
Check 'install-payload-matches' (PayloadMatches $Candidate) "$((Payloads $Candidate).Count) payloads"
$receipt = Get-Content -Raw -LiteralPath (Join-Path $game 'BepInEx\WodenWheel-install.json') | ConvertFrom-Json
Check 'receipt-curvedTriples' ($receipt.curvedTriples -eq $true) $receipt.version
$d = Compare-Snapshot $pristine (Snapshot)
Check 'install-touches-no-game-file' ($d.added.Count -ge 10 -and $d.removed.Count -eq 0 -and $d.changed.Count -eq 0) "added $($d.added.Count)"

$r = Run $Candidate 'Uninstall'
Check 'uninstall-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
$after = Snapshot
$d = Compare-Snapshot $pristine $after
$payloadLeft = @(Payloads $Candidate | Where-Object { Test-Path -LiteralPath (Join-Path $game $_.path) } | ForEach-Object { $_.path })
Check 'uninstall-keeps-game-files' ($d.removed.Count -eq 0 -and $d.changed.Count -eq 0) "removed [$($d.removed -join ', ')] changed [$($d.changed -join ', ')]"
Check 'uninstall-removes-mod-payloads' ($payloadLeft.Count -eq 0) ($payloadLeft -join ', ')
$unexpected = @($d.added | Where-Object { $_ -notmatch '^(BepInEx\\(core|config|WodenWheel-install\.json)|dotnet\\|WodenWheelBackups\\|winhttp\.dll$|doorstop_config\.ini$|\.doorstop_version$|changelog\.txt$)' })
Check 'uninstall-leftovers-are-loader-backups-receipt' ($unexpected.Count -eq 0) ("unexpected [" + ($unexpected -join ', ') + "]")
$results['uninstallLeftoverCount'] = $d.added.Count

$r = Run $Candidate
Check 'reinstall-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'reinstall-payload-matches' (PayloadMatches $Candidate)

$r = Run $Previous
Check 'rollback-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'rollback-payload-is-previous' (PayloadMatches $Previous)

$r = Run $Candidate
Check 'forward-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'forward-payload-is-candidate' (PayloadMatches $Candidate)
$d = Compare-Snapshot $pristine (Snapshot)
Check 'game-files-unchanged-at-end' ($d.removed.Count -eq 0 -and $d.changed.Count -eq 0)

$results['failures'] = $failures
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Root 'phaseA-result.json') -Encoding UTF8
if ($failures.Count) { Write-Host "Phase A FAIL ($($failures.Count))"; exit 1 }
Write-Host 'Phase A PASS'
