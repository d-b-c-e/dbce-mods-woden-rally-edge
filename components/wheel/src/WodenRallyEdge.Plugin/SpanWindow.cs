using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Configuration;
using Dbce.Wheel.Display;
using HarmonyLib;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

/// <summary>
/// STD-015: triples on separate monitors without Surround (port of DRIVE's SpanWindow). When the toolkit
/// racing-row policy finds the primary display with one equal-size display directly on each side
/// (<see cref="SpanTarget"/>; other displays above or below, such as a diagnostic screen, are ignored), the
/// game's own startup apply (<c>ResolutionManager.FirstResolutionSet</c>) becomes one windowed apply at that
/// row's span; the window is then made borderless and placed over the row, and Unity follows the window
/// size, so Auto triples start.
/// Without a span, a game window that Unity opened on a display outside the racing row (its saved monitor preference
/// naming the dashboard display) is moved the same way onto the row's primary display (<see cref="WindowPlacement"/>):
/// in the startup apply, and after a display change once two checks 2 s apart agree.
/// </summary>
/// <remarks>
/// Only <c>Screen.SetResolution</c> calls made while <c>FirstResolutionSet</c> runs are changed, so it
/// stays the game's single apply (STD-008; stacked runtime changes at 7680 reset the GPU driver on
/// 2026-10-04). The window is never activated or raised. After placement it is checked every 2 s
/// (Unity can put the frame back) until the player chooses another mode or size in Options.
/// </remarks>
internal static class SpanWindow
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hwnd, StringBuilder name, int count);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect32 rect);
    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct Rect32 { public int Left, Top, Right, Bottom; }

    private const int GWL_STYLE = -16;
    private const long WS_POPUP = 0x80000000L, WS_VISIBLE = 0x10000000L, WS_CAPTION = 0x00C00000L, WS_THICKFRAME = 0x00040000L,
        WS_MINIMIZEBOX = 0x00020000L, WS_MAXIMIZEBOX = 0x00010000L, WS_SYSMENU = 0x00080000L;
    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020, SWP_NOOWNERZORDER = 0x0200;

    private static ConfigEntry<bool> _enabled = null!;
    private static Func<bool> _triplesAllowed = () => true;
    private static readonly SpanTarget _target = new(WindowsMonitors.SelectRacingRow);
    private static int _attempts, _depth, _offRowSeen;
    private static bool _maintain, _startupDone, _rowApplied, _rowHeld;
    private static float _next, _nextCheck, _nextRowCheck;
    // Set while the window is kept on the row's primary display instead of a span.
    private static (int X, int Y, int W, int H)? _move;
    private static string? _placementReason;
    internal static string Status { get; private set; } = "not needed";
    /// <summary>The saved separate-monitor span setting (F6 Cameras selector, STD-022); applies at the next start.</summary>
    internal static bool Enabled { get => _enabled != null && _enabled.Value; set { if (_enabled != null && _enabled.Value != value) _enabled.Value = value; } }

    internal static void Bind(ConfigFile cfg, Func<bool> triplesAllowed)
    {
        _triplesAllowed = triplesAllowed;
        // On by default since the 2026-10-06 qualification (Kenya replay, 3,601 poses at 0.06 mm, triples
        // on the separate-monitor span); an explicit false in the config is kept.
        _enabled = cfg.Bind("Triple", "SpanSeparateMonitors", true, "With three side-by-side monitors and no Surround, open the game as one borderless window across all of them so the triple views can run (STD-015). Set false to keep the stock single screen.");
    }

    /// <summary>The racing row's span when it should be used, else null (SpanTarget; the reason is logged once per change).</summary>
    internal static (int X, int Y, int W, int H)? Target()
    {
        var span = _target.Decide(_enabled.Value, _triplesAllowed(), out bool changed);
        if (changed) Runtime.Log.LogInfo("Span window: " + (span == null ? "not used: " : "racing row: ") + _target.Reason);
        return span;
    }

    /// <summary>The span, else the primary display the window was moved onto.</summary>
    private static (int X, int Y, int W, int H)? Goal() => Target() ?? _move;
    private static string Label => _move != null ? "Window placement" : "Span window";

    /// <summary>The row's primary display when the game window is on a display outside the racing row (logged once per change).</summary>
    private static (int X, int Y, int W, int H)? OffRow()
    {
        var hwnd = FindGameWindow();
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return null;
        var target = WindowPlacement.Decide(WindowsMonitors.Read(), (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top), out string reason);
        if (reason != _placementReason) { _placementReason = reason; Runtime.Log.LogInfo("Window placement: " + reason); }
        return target;
    }

    internal static void BeginApply() => _depth++;
    internal static void EndApply() { if (_depth > 0) _depth--; }

    /// <summary>Inside the game's startup apply: substitute the span, windowed. Returns true when changed.</summary>
    internal static bool Substitute(ref int width, ref int height, ref bool fullscreen)
    {
        if (_depth == 0) return false;
        var target = Target();
        if (target == null)
        {
            try { _move = OffRow(); } catch (Exception ex) { _move = null; Runtime.Log.LogWarning("Window placement: " + ex.Message); }
            target = _move;
        }
        else _move = null;
        if (target == null) return false;
        _rowApplied = _move != null; // this apply is the off-row episode's one windowed apply
        var (sx, sy, sw, sh) = target.Value;
        Runtime.Log.LogInfo(_move != null
            ? $"Window placement: {sw}x{sh} borderless window on the racing row's primary display at ({sx},{sy}); windowed instead of {width}x{height} fullscreen {fullscreen}"
            : $"Span window: {sw}x{sh} span over separate monitors at ({sx},{sy}); windowed instead of {width}x{height} fullscreen {fullscreen}");
        width = sw; height = sh; fullscreen = false;
        _attempts = 6; _next = Time.unscaledTime + 0.5f; _maintain = false; Status = "placing";
        return true;
    }

    /// <summary>Per frame from Runtime.Update.</summary>
    internal static void Tick()
    {
        if (_attempts <= 0) { Maintain(); RowCheck(); return; }
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 1.5f; _attempts--;
        try
        {
            var target = Goal();
            if (target == null) { _attempts = 0; Status = "not needed"; return; }
            var (x, y, w, h) = target.Value;
            var hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero) { Status = "waiting for the game window"; return; }
            Place(hwnd, x, y, w, h);
            bool done = Screen.width == w && Screen.height == h;
            Status = done ? (_move != null ? "on the racing row" : "spanning") + $" {w}x{h} at ({x},{y})" : $"placing ({Screen.width}x{Screen.height} so far)";
            Runtime.Log.LogInfo($"{Label}: {Status}, attempt {6 - _attempts}");
            if (done && _attempts < 4) { _attempts = 0; _maintain = true; _nextCheck = Time.unscaledTime + 2f; }
        }
        catch (Exception ex) { _attempts = 0; Status = "failed: " + ex.Message; Runtime.Log.LogWarning(Label + " " + Status); }
    }

    private static void Maintain()
    {
        if (!_maintain || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 2f;
        try
        {
            var target = Goal();
            if (target == null || Screen.fullScreen) { _maintain = false; return; }
            var (x, y, w, h) = target.Value;
            if (Math.Abs(Screen.width - w) > 100 || Math.Abs(Screen.height - h) > 100) { _maintain = false; Status = "left to the game (another size chosen)"; return; }
            var hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero) return;
            if (Place(hwnd, x, y, w, h)) Runtime.Log.LogInfo(_move != null ? "Window placement: restored the window on the racing row" : "Span window: restored the span");
        }
        catch (Exception ex) { _maintain = false; Runtime.Log.LogWarning(Label + " maintain: " + ex.Message); }
    }

    /// <summary>After the game's first apply and without a span: every 2 s, move a window that a display change left
    /// outside the racing row. Two agreeing checks first, so a topology still settling is not chased; leaving
    /// fullscreen there takes one windowed apply, as the startup substitution does.</summary>
    private static void RowCheck()
    {
        if (!_startupDone || _maintain || Time.unscaledTime < _nextRowCheck) return;
        _nextRowCheck = Time.unscaledTime + 2f;
        try
        {
            if (Target() != null) { _offRowSeen = 0; return; }
            var target = OffRow();
            if (target == null) { _offRowSeen = 0; _rowApplied = _rowHeld = false; return; }
            if (_rowHeld || ++_offRowSeen < 2) return;
            _offRowSeen = 0; _move = target;
            var (_, _, w, h) = target.Value;
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                // At most one runtime apply per off-row episode: stacked changes at 7680 reset the GPU driver (STD-008).
                if (_rowApplied) { _rowHeld = true; Status = "fullscreen on a display outside the racing row"; Runtime.Log.LogWarning("Window placement: still fullscreen outside the racing row after one windowed apply; left as is until it is back on the row"); return; }
                _rowApplied = true;
                Runtime.Log.LogInfo($"Window placement: one windowed {w}x{h} apply to leave {Screen.fullScreenMode} on the wrong display");
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
            }
            _attempts = 6; _next = Time.unscaledTime + 0.5f; Status = "placing";
        }
        catch (Exception ex) { _offRowSeen = 0; _nextRowCheck = Time.unscaledTime + 30f; Runtime.Log.LogWarning("Window placement: " + ex.Message); }
    }

    /// <summary>Borderless popup over the span; true when anything had to change.</summary>
    private static bool Place(IntPtr hwnd, int x, int y, int w, int h)
    {
        long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        long popup = (style | WS_POPUP | WS_VISIBLE) & ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        GetWindowRect(hwnd, out var r);
        bool placed = r.Left == x && r.Top == y && r.Right - r.Left == w && r.Bottom - r.Top == h;
        if (popup == style && placed) return false;
        if (popup != style) SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(popup));
        SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_FRAMECHANGED);
        return true;
    }

    /// <summary>Patched one by one after PatchAll, so a missing overload disables only this.</summary>
    internal static void Apply(Harmony harmony)
    {
        void Try(string what, Action patch)
        {
            try { patch(); }
            catch (Exception ex) { Runtime.Log.LogWarning($"Span window: {what} not patched: {ex.Message}"); }
        }
        Try("FirstResolutionSet scope", () => harmony.Patch(AccessTools.Method(typeof(ResolutionManager), nameof(ResolutionManager.FirstResolutionSet)),
            prefix: new HarmonyMethod(typeof(SpanWindow), nameof(ScopePrefix)), finalizer: new HarmonyMethod(typeof(SpanWindow), nameof(ScopeFinalizer))));
        Try("SetResolution(int,int,bool)", () => harmony.Patch(AccessTools.Method(typeof(Screen), nameof(Screen.SetResolution), new[] { typeof(int), typeof(int), typeof(bool) }),
            prefix: new HarmonyMethod(typeof(SpanWindow), nameof(BoolPrefix))));
        Try("SetResolution(int,int,FullScreenMode)", () => harmony.Patch(AccessTools.Method(typeof(Screen), nameof(Screen.SetResolution), new[] { typeof(int), typeof(int), typeof(FullScreenMode) }),
            prefix: new HarmonyMethod(typeof(SpanWindow), nameof(ModePrefix))));
    }

    private static void ScopePrefix() => BeginApply();
    private static Exception? ScopeFinalizer(Exception? __exception) { EndApply(); _startupDone = true; return __exception; }
    private static void BoolPrefix(ref int width, ref int height, ref bool fullscreen) { try { Substitute(ref width, ref height, ref fullscreen); } catch { } }
    private static void ModePrefix(ref int width, ref int height, ref FullScreenMode fullscreenMode)
    {
        try
        {
            bool fullscreen = fullscreenMode != FullScreenMode.Windowed;
            if (Substitute(ref width, ref height, ref fullscreen)) fullscreenMode = FullScreenMode.Windowed;
        }
        catch { }
    }

    private static IntPtr FindGameWindow()
    {
        uint pid = (uint)Environment.ProcessId;
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint owner);
            if (owner != pid) return true;
            var name = new StringBuilder(64);
            GetClassNameW(hwnd, name, name.Capacity);
            if (name.ToString() != "UnityWndClass") return true;
            found = hwnd; return false;
        }, IntPtr.Zero);
        return found;
    }
}
