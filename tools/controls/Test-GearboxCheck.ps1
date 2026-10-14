# Offline fixtures for Run-TemporaryGearboxCheck.ps1 (sana 96509), on the runner's own functions and finally block:
# status classification on the production plugin's exact status text, stale-log isolation, and cleanup that always
# attempts the owner restore. No game, lease or owner file. Pass the WodenRallyEdge.Core.dll built from the same source
# (for example the plugin build's output folder): its NativeGearboxPreference supplies the status text.
#   pwsh -NoProfile -File tools/controls/Test-GearboxCheck.ps1 -CoreDll <built>/WodenRallyEdge.Core.dll
param([Parameter(Mandatory)][string]$CoreDll, [string]$Runner = (Join-Path $PSScriptRoot 'Run-TemporaryGearboxCheck.ps1'))
$ErrorActionPreference = 'Stop'
$t = $null; $e = $null; $ast = [Management.Automation.Language.Parser]::ParseFile($Runner, [ref]$t, [ref]$e); if ($e.Count) { throw 'runner parse errors' }
foreach ($name in 'Classify-GearboxLine', 'Gearbox-Line', 'Clear-SessionLog') {
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

# 2. Stale isolation: an old log's status line never survives the pre-launch clear; a missing log reads as no line.
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

    # 3. Cleanup: the runner's own finally block with stubbed owner operations.
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
        $game = $case.Game; $mutated = $case.Mutated; $restored = $false; $failure = $null; $states = [Collections.Generic.List[object]]::new()
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
        $threw = $null; try { . ([scriptblock]::Create($finally)) } catch { $threw = $_.Exception.Message }
        $notesFile = Join-Path $Result 'cleanup-notes.txt'; $noteText = if (Test-Path $notesFile) { Get-Content -Raw $notesFile } else { '' }
        Check "$($case.Name): finally block did not throw" ($null -eq $threw)
        Check "$($case.Name): restore $(if ($case.Restore) { 'attempted and verified' } else { 'not done' })" ((($script:restoreCalls -eq 1) -eq $case.Restore) -and ($restored -eq $case.Restore))
        Check "$($case.Name): lease $(if ($case.Lease) { 'released' } else { 'kept' })" (($script:leaseExits -eq 1) -eq $case.Lease)
        Check "$($case.Name): notes recorded" (@($case.Notes | Where-Object { $noteText -notlike "*$_*" }).Count -eq 0)
        if ($case.Restore) { Check "$($case.Name): restore-result.json written" (Test-Path (Join-Path $Result 'restore-result.json')) }
    }
} finally { Remove-Item -LiteralPath $tmp -Recurse -Force }
"failures: $fail"; if ($fail) { exit 1 }
