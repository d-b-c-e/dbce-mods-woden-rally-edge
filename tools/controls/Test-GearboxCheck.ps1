# Offline fixtures for Run-TemporaryGearboxCheck.ps1 (sana 96509), on the runner's own functions and finally block:
# status classification on the production plugin's exact status text, save-state comparison on recorded hashes,
# stale-log isolation, and cleanup that always
# attempts the owner restore. No game, lease or owner file. Pass the WodenRallyEdge.Core.dll built from the same source
# (for example the plugin build's output folder): its NativeGearboxPreference supplies the status text.
#   pwsh -NoProfile -File tools/controls/Test-GearboxCheck.ps1 -CoreDll <built>/WodenRallyEdge.Core.dll
param([Parameter(Mandatory)][string]$CoreDll, [string]$Runner = (Join-Path $PSScriptRoot 'Run-TemporaryGearboxCheck.ps1'))
$ErrorActionPreference = 'Stop'
$t = $null; $e = $null; $ast = [Management.Automation.Language.Parser]::ParseFile($Runner, [ref]$t, [ref]$e); if ($e.Count) { throw 'runner parse errors' }
foreach ($name in 'Classify-GearboxLine', 'Gearbox-Line', 'Clear-SessionLog', 'Compare-SaveStates') {
    . ([scriptblock]::Create(($ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true) | Select-Object -First 1).Extent.Text))
}
$finally = ($ast.FindAll({ param($n) $n -is [Management.Automation.Language.TryStatementAst] -and $n.Finally }, $true) | Select-Object -Last 1).Finally.Extent.Text; $finally = $finally.Substring(1, $finally.Length - 2)
$fail = 0
function Check([string]$Name, [bool]$Ok) { if ($Ok) { "PASS $Name" } else { "FAIL $Name"; $script:fail++ } }

# 1. Classification on the plugin's own text: NativeGearboxPreference from the built Core, prefixed as Finish logs it.
[void][Reflection.Assembly]::LoadFrom($CoreDll)
$P = [WodenRallyEdge.Core.NativeGearboxPreference]
function Line([string]$Status, [string]$Level = 'Info   ') { "[$Level:Woden Rally Edge Wheel] Native gearbox $Status" }
function Applied($plan) { $plan.Status + $(if ($plan.Writes.Count) { ' (' + (($plan.Writes | ForEach-Object { $P::Describe($_) }) -join ', ') + ')' } else { '' }) }
$all = [bool[]]@($true, $true, $true, $true); $man = [bool[]]@($false, $false, $false, $false)
$setManual = Line (Applied ($P::Decide('Manual', 0, $false, $all, $all)))
$setAuto = Line (Applied ($P::Decide('Automatic', 0, $false, $man, $man)))
$matched = Line (Applied ($P::Decide('Manual', 0, $false, $man, $man)))
$tooling = Line ($P::Decide('Manual', 0, $true, $all, $all).Status) 'Warning'
$w = $P::Decide('Manual', 0, $false, $all, $all).Writes
$incomplete = Line ($P::FailedStatus([WodenRallyEdge.Core.NativeGearboxPreference+Write[]]@($w[0]), $w[1], 'read back Automatic')) 'Warning'
$closed = Line 'not applied: the first main menu closed before the game''s saved choice loaded' 'Warning'
foreach ($c in @(
    @($setManual, 'Manual', 'set'), @($setAuto, 'Automatic', 'set'), @($setManual, 'Automatic', 'failed'),
    @($matched, 'Manual', 'already-matched'), @($matched, 'Automatic', 'failed'), @($tooling, 'Manual', 'failed'),
    @($incomplete, 'Manual', 'failed'), @($closed, 'Manual', 'failed'), @($null, 'Manual', 'missing'))) {
    $got = Classify-GearboxLine $c[0] $c[1]
    Check "classify [$($c[1])] $(if ($c[0]) { ($c[0] -split 'Native gearbox ', 2)[1].Substring(0, [Math]::Min(60, (($c[0] -split 'Native gearbox ', 2)[1]).Length)) } else { '(no line)' }) -> $got" ($got -eq $c[2])
}

# 2. Save-state comparison (sana 100666) on the hashes recorded by the 10-14 runs (gearbox-check-manual-20261014-02/-03).
$ownerSha256 = 'EBFA4FF628B22456C142E8236C0AC984B17EC7A3D4775FAB1CE8FF6D3C8F3290'; $ownerSha1 = '688b5551f127d5a07b842d16df8ee19d5c44f31c'
$runSha256 = '34C1110468C2CD91302C01B911B542FD521466AEC6F94D00498F44003126AB84'; $runSha1 = '0a4b556f162f9559e5a19119a39b646f2ce8f6b6'
$ownerBackups = @('Supersave0_2026-10-06_20-55-47.sav A3D868764A8A37D2DCB7C9AF008DB2E793A758DCCC868A028F5389E756A30265', 'Supersave0_2026-10-06_20-55-53.sav EBFA4FF628B22456C142E8236C0AC984B17EC7A3D4775FAB1CE8FF6D3C8F3290')
function Sample($Sha256, $Sha1, $Backups, $Cloud) { [ordered]@{ supersave = [ordered]@{ sha256 = $Sha256; sha1 = $Sha1 }; backups = $Backups; cloudSaveSha1 = $Cloud } }
$ownerBefore = Sample $ownerSha256 $ownerSha1 $ownerBackups $runSha1
# 2a. Run 03: local save and backups unchanged; Steam's record moved from run 02's save to the owner's file.
Check 'run 03: record moved to the unchanged local save -> cloud-reconciled' ((Compare-SaveStates $ownerBefore (Sample $ownerSha256 $ownerSha1 $ownerBackups $ownerSha1)) -eq 'cloud-reconciled')
Check 'no change at all -> unchanged' ((Compare-SaveStates $ownerBefore (Sample $ownerSha256 $ownerSha1 $ownerBackups $runSha1)) -eq 'unchanged')
# 2b. A record equal to the local file is not enough: run 02's shape (local save rewritten, backup rotated, record =
# the new file) and an unchanged local save whose record moved to some other file are both saves.
$rotated = @($ownerBackups[1], "Supersave0_2026-10-14_06-29-43.sav $runSha256")
Check 'run 02: local rewritten and record = the new file -> saved' ((Compare-SaveStates (Sample $ownerSha256 $ownerSha1 $ownerBackups $ownerSha1) (Sample $runSha256 $runSha1 $rotated $runSha1)) -eq 'saved')
Check 'local unchanged, record moved to another file -> saved' ((Compare-SaveStates (Sample $ownerSha256 $ownerSha1 $ownerBackups $ownerSha1) (Sample $ownerSha256 $ownerSha1 $ownerBackups $runSha1)) -eq 'saved')
Check 'backups changed, record = the unchanged local save -> saved' ((Compare-SaveStates $ownerBefore (Sample $ownerSha256 $ownerSha1 $rotated $ownerSha1)) -eq 'saved')
# 2c. Incomplete evidence stays a failure: a sample without SHA-1 (the run 02 format), a vanished record, no sample.
Check 'record moved but a sample lacks SHA-1 -> unknown' ((Compare-SaveStates $ownerBefore (Sample $ownerSha256 $null $ownerBackups $ownerSha1)) -eq 'unknown')
Check 'record vanished -> unknown' ((Compare-SaveStates $ownerBefore (Sample $ownerSha256 $ownerSha1 $ownerBackups $null)) -eq 'unknown')
Check 'empty sample -> unknown' ((Compare-SaveStates $ownerBefore @{}) -eq 'unknown')

# 3. Stale isolation: an old log's status line never survives the pre-launch clear; a missing log reads as no line.
$tmp = Join-Path $env:TEMP ('gearbox-runner-' + [guid]::NewGuid().ToString('N')); New-Item -ItemType Directory $tmp | Out-Null
try {
    $log = Join-Path $tmp 'LogOutput.log'
    [IO.File]::WriteAllText($log, "old session`r`n$setManual`r`n")
    Check 'an old log line is visible before the clear' ((Gearbox-Line $log) -eq $setManual)
    Clear-SessionLog $log
    Check 'after the pre-launch clear no old status line passes' ($null -eq (Gearbox-Line $log))
    [IO.File]::AppendAllText($log, "new session`r`n$matched`r`n")
    Check 'a line written after the clear is the one read' ((Gearbox-Line $log) -eq $matched)
    Check 'a missing log reads as no line' ($null -eq (Gearbox-Line (Join-Path $tmp 'absent.log')))

    # 4. Cleanup: the runner's own finally block with stubbed owner operations.
    function New-Game([bool]$Exited, [bool]$CloseThrows, [bool]$Exits) {
        $g = [pscustomobject]@{ HasExited = $Exited; ExitCode = 0; CloseThrows = $CloseThrows; Exits = $Exits }
        $g | Add-Member ScriptMethod CloseMainWindow { if ($this.CloseThrows) { throw 'close failed' }; $true }
        $g | Add-Member ScriptMethod WaitForExit { param($ms) if ($this.Exits) { $this.HasExited = $true }; $this.HasExited }
        $g
    }
    foreach ($case in @(
        @{ Name = 'save-state and log-copy failures still restore'; Game = (New-Game $false $false $true); SaveThrows = $true; CopyThrows = $true; Running = $false; Mutated = $true; Restore = $true; Lease = $true; Notes = @('save state:', 'log copy:') },
        @{ Name = 'a close that throws still restores once the game is gone'; Game = (New-Game $false $true $false); SaveThrows = $false; CopyThrows = $false; Running = $false; Mutated = $true; Restore = $true; Lease = $true; Notes = @('close:') },
        @{ Name = 'a game that will not close keeps the lease and restores nothing'; Game = (New-Game $false $false $false); SaveThrows = $false; CopyThrows = $false; Running = $true; Mutated = $true; Restore = $false; Lease = $false; Notes = @('did not close') },
        @{ Name = 'nothing mutated: no restore, lease released'; Game = $null; SaveThrows = $true; CopyThrows = $true; Running = $false; Mutated = $false; Restore = $false; Lease = $true; Notes = @() })) {
        $Result = Join-Path $tmp ([guid]::NewGuid().ToString('N')); New-Item -ItemType Directory $Result | Out-Null
        $backups = Join-Path $tmp 'backups'; New-Item -ItemType Directory -Force $backups | Out-Null; $backupBefore = @()
        $script:restoreCalls = 0; $script:leaseExits = 0; $script:case = $case
        $game = $case.Game; $mutated = $case.Mutated; $restored = $false; $failure = $null; $saveState = $null; $states = [Collections.Generic.List[object]]::new()
        $line = $setManual; $outcome = 'set'; $Setting = 'Manual'; $OwnerReceiptSha256 = 'R'; $prefsHash = 'P'; $before = [pscustomobject]@{ entries = @(1, 2) }
        $logPath = $log; $slot = 'slot'; $lease = [pscustomobject]@{ token = 't' }; $roots = @{}; $files = @{}; $receiptPath = 'r'
        function Closed { if ($script:case.Running) { throw 'Woden must be closed; retain owner state and lease.' } }
        function Assert-StageRigLease { }
        function Save-State { if ($script:case.SaveThrows) { throw 'save failed' }; @{} }
        function Copy-Item { if ($script:case.CopyThrows) { throw 'copy failed' } }
        function Restore-ControlFiles { $script:restoreCalls++ }
        function Get-ControlHash { 'P' }
        function Registry-Restore { }; function Registry-Snapshot { }; function Assert-Inventory { }; function Pin { }
        function Exit-StageRigLease { $script:leaseExits++ }
        function Start-Sleep { }
        $suppressPath = $null; $suppressId = $null
        $threw = $null; try { . ([scriptblock]::Create($finally)) } catch { $threw = $_.Exception.Message }
        $notesFile = Join-Path $Result 'cleanup-notes.txt'; $noteText = if (Test-Path $notesFile) { Get-Content -Raw $notesFile } else { '' }
        Check "$($case.Name): finally block did not throw" ($null -eq $threw)
        Check "$($case.Name): restore $(if ($case.Restore) { 'attempted and verified' } else { 'not done' })" ((($script:restoreCalls -eq 1) -eq $case.Restore) -and ($restored -eq $case.Restore))
        Check "$($case.Name): lease $(if ($case.Lease) { 'released' } else { 'kept' })" (($script:leaseExits -eq 1) -eq $case.Lease)
        Check "$($case.Name): notes recorded" (@($case.Notes | Where-Object { $noteText -notlike "*$_*" }).Count -eq 0)
        if ($case.Restore) { Check "$($case.Name): restore-result.json written" (Test-Path (Join-Path $Result 'restore-result.json')) }
        if ($case.Mutated -and $case.SaveThrows) { Check "$($case.Name): save state unknown and the run failed" (($saveState -eq 'unknown') -and $failure) }
    }
} finally { Remove-Item -LiteralPath $tmp -Recurse -Force }
"failures: $fail"; if ($fail) { exit 1 }
