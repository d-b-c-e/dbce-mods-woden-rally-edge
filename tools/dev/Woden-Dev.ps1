<#
.SYNOPSIS
    Development loop for Woden without the owner at the rig: launch, span a three-screen
    window without Surround, send dev-input commands, fetch screenshots.
    Needs [Dev] InputCommandFile = true. Never use for owner sessions.

.EXAMPLE
    .\tools\dev\Woden-Dev.ps1 -Launch -Span
    .\tools\dev\Woden-Dev.ps1 -Cmd 'press Enter','shot menu' -Wait 3
#>
[CmdletBinding()]
param([switch]$Launch, [switch]$Span, [string[]]$Cmd, [string[]]$Keys, [double]$Wait = 2, [string]$Out)
$ErrorActionPreference = 'Stop'
$game = 'D:\Program Files (x86)\Steam\steamapps\common\Super Woden Rally Edge'
$dev = Join-Path $game 'BepInEx\plugins\WodenRallyEdgeWheel\dev'
$log = Join-Path $game 'BepInEx\LogOutput.log'
$srwe = 'E:\Source\toolkits\srwe-cli\SRWE.Cli\bin\Release\net10.0-windows\srwe-cli.exe'
if (-not $Out) { $Out = Join-Path $env:TEMP 'woden-dev' }
New-Item -ItemType Directory -Force -Path $Out | Out-Null

Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public static class WodenWin { public delegate bool P(IntPtr h, IntPtr l);
[DllImport("user32.dll")] static extern bool EnumWindows(P p, IntPtr l);
[DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint f, UIntPtr e);
public static IntPtr Find(uint target, string cls){ IntPtr found=IntPtr.Zero; EnumWindows((h,l)=>{ uint pid; GetWindowThreadProcessId(h,out pid); var s=new StringBuilder(256); GetClassName(h,s,256); if(pid==target && s.ToString()==cls){ found=h; return false; } return true;}, IntPtr.Zero); return found; } }
'@

function Get-Game { Get-Process 'Super Woden Rally Edge' -ErrorAction SilentlyContinue | Select-Object -First 1 }

if ($Launch) {
    if (Get-Game) { throw 'Woden is already running.' }
    Start-Process 'D:\Program Files (x86)\Steam\steam.exe' -ArgumentList '-applaunch', '3218630'
    $deadline = (Get-Date).AddMinutes(2)
    while (-not (Get-Game)) { if ((Get-Date) -gt $deadline) { throw 'Woden did not start.' }; Start-Sleep 1 }
    Start-Sleep 15
}
if ($Span) {
    $p = Get-Game; if (-not $p) { throw 'Woden is not running.' }
    Set-Content -LiteralPath (Join-Path $dev 'cmd.txt') -Value 'window 7680 1440'
    Start-Sleep 3
    $hwnd = [WodenWin]::Find([uint32]$p.Id, 'UnityWndClass')
    & $srwe apply --hwnd ('0x{0:X}' -f $hwnd.ToInt64()) --x -2560 --y 0 --width 7680 --height 1440 --borderless --client-area --json | Out-Null
    $console = [WodenWin]::Find([uint32]$p.Id, 'ConsoleWindowClass')
    if ($console -ne [IntPtr]::Zero) { & $srwe apply --hwnd ('0x{0:X}' -f $console.ToInt64()) --x 0 --y 800 --width 1600 --height 600 --json | Out-Null }
    Start-Sleep 2
    Set-Content -LiteralPath (Join-Path $dev 'cmd.txt') -Value 'status'
    Start-Sleep 1
}
if ($Keys) {
    # Real OS key presses for screens that read legacy Input (startup, title). Takes focus;
    # owner-away development only.
    $p = Get-Game; if (-not $p) { throw 'Woden is not running.' }
    $vk = @{ Enter = 0x0D; Escape = 0x1B; Space = 0x20; Left = 0x25; Up = 0x26; Right = 0x27; Down = 0x28; Backspace = 0x08 }
    [WodenWin]::SetForegroundWindow([WodenWin]::Find([uint32]$p.Id, 'UnityWndClass')) | Out-Null
    Start-Sleep -Milliseconds 300
    foreach ($entry in $Keys) {
        # Key or Key:milliseconds to hold (menus with a free cursor need longer holds).
        $k, $holdMs = $entry.Split(':'); $holdMs = if ($holdMs) { [int]$holdMs } else { 120 }
        $code = if ($vk.ContainsKey($k)) { $vk[$k] } elseif ($k.Length -eq 1) { [byte][char]$k.ToUpper() } else { throw "Unknown key $k" }
        [WodenWin]::keybd_event([byte]$code, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds $holdMs
        [WodenWin]::keybd_event([byte]$code, 0, 2, [UIntPtr]::Zero); Start-Sleep -Milliseconds 450
    }
}
if ($Cmd) {
    $before = (Get-Item $log).Length
    Set-Content -LiteralPath (Join-Path $dev 'cmd.txt') -Value $Cmd
    Start-Sleep -Milliseconds ([int]($Wait * 1000))
    foreach ($line in $Cmd) {
        if ($line -match '^shot\s+(\S+)') {
            $png = Join-Path $dev "$($Matches[1]).png"
            $deadline = (Get-Date).AddSeconds(10)
            while (-not (Test-Path $png) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 300 }
            if (Test-Path $png) { Move-Item -Force $png (Join-Path $Out "$($Matches[1]).png"); Write-Output (Join-Path $Out "$($Matches[1]).png") }
        }
    }
}
# New log lines from the plugin since this call.
$stream = [IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
try {
    if ($Cmd) { $stream.Seek($before, 'Begin') | Out-Null } else { $stream.Seek([Math]::Max(0, $stream.Length - 4000), 'Begin') | Out-Null }
    (New-Object IO.StreamReader $stream).ReadToEnd() -split "`n" | Where-Object { $_ -match 'Woden Rally Edge Wheel' -and $_ -notmatch 'hooks=' } | Select-Object -Last 15
} finally { $stream.Dispose() }
