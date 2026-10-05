<#
.SYNOPSIS
    Guards a display or resolution test. Kills the game and restores the owner's monitor
    profile at the first NVIDIA driver reset, or when the game stops producing frames.

.DESCRIPTION
    On 2026-10-04 stacked resolution changes at 7680x1440 reset the driver twice and the
    machine needed a hard reboot. Run this before any display test and leave it running;
    it exits by itself when the game closes. Trips are written to the -Log file.

.EXAMPLE
    Start-Process pwsh -WindowStyle Hidden -ArgumentList '-File', '.\tools\dev\Display-Watchdog.ps1'
#>
[CmdletBinding()]
param(
    [string]$Process = 'Super Woden Rally Edge',
    [string]$Heartbeat = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge\BepInEx\plugins\WodenRallyEdgeWheel\dev\heartbeat.txt',
    [string]$RestoreProfile = 'Sim Racing',
    [int]$StallSeconds = 30,
    [int]$StartupSeconds = 120,
    [string]$Log = (Join-Path $env:TEMP 'display-watchdog.log')
)
$switcher = 'E:\Source\toolkits\monitor-configuration-hotkey\publish\MonitorProfileSwitcher.exe'
$started = Get-Date
function Write-Log([string]$Text) { "$(Get-Date -Format o) $Text" | Add-Content -LiteralPath $Log }

function Trip([string]$Reason) {
    Write-Log "TRIP: $Reason"
    Get-Process -Name $Process -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep 5
    # The switcher can hang if the display stack is wedged; never wait on it forever.
    $p = Start-Process $switcher -ArgumentList '--ipc', 'apply', "`"$RestoreProfile`"" -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit(60000)) { Write-Log 'profile restore did not finish in 60 s' } else { Write-Log "profile '$RestoreProfile' restore exit $($p.ExitCode)" }
    exit 1
}

Write-Log "watching '$Process' (stall $StallSeconds s, restore '$RestoreProfile')"
$seen = $false; $lastFrame = -1; $lastMove = Get-Date
while ($true) {
    Start-Sleep 2
    $resets = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'nvlddmkm'; StartTime = $started } -ErrorAction SilentlyContinue)
    if ($resets.Count -gt 0) { Trip "nvlddmkm event $($resets[0].Id) at $($resets[0].TimeCreated)" }

    $game = Get-Process -Name $Process -ErrorAction SilentlyContinue
    if (-not $game) {
        if ($seen) { Write-Log 'game closed; watchdog done'; exit 0 }
        if ((Get-Date) - $started -gt [TimeSpan]::FromSeconds($StartupSeconds)) { Write-Log 'game never started; watchdog done'; exit 0 }
        continue
    }
    $seen = $true
    $frame = -1
    try { $frame = [long]((Get-Content -LiteralPath $Heartbeat -Raw -ErrorAction Stop).Split(' ')[0]) } catch { }
    if ($frame -ne $lastFrame) { $lastFrame = $frame; $lastMove = Get-Date; continue }
    $limit = if ($frame -lt 0) { $StartupSeconds } else { $StallSeconds }
    if ((Get-Date) - $lastMove -gt [TimeSpan]::FromSeconds($limit)) { Trip "no new frames for $limit s (last frame $frame)" }
}
