using System.Runtime.InteropServices;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// STD-015: triples on separate monitors without Surround (port of DRIVE's SpanWindow). When Windows
/// shows three or more side-by-side monitors at one height and the main display is a single
/// screen, the game's own startup apply (<c>ResolutionManager.FirstResolutionSet</c>) becomes one
/// windowed apply at the virtual-desktop span; the window is then made borderless and placed over
/// the span, and Unity follows the window size, so Auto triples start.
/// </summary>
/// <remarks>
/// Only <c>Screen.SetResolution</c> calls made while <c>FirstResolutionSet</c> runs are changed, so it
/// stays the game's single apply (STD-008; stacked runtime changes at 7680 reset the GPU driver on
/// 2026-10-04). The window is never activated or raised. After placement it is checked every 2 s
/// (Unity can put the frame back) until the player chooses another mode or size in Options.
/// </remarks>
internal static class SpanWindow
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
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
    private static int _attempts, _depth;
    private static bool _maintain;
    private static float _next, _nextCheck;
    internal static string Status { get; private set; } = "not needed";

    internal static void Bind(ConfigFile cfg, Func<bool> triplesAllowed)
    {
        _triplesAllowed = triplesAllowed;
        // On by default since the 2026-10-06 qualification (Kenya replay, 3,601 poses at 0.06 mm, triples
        // on the separate-monitor span); an explicit false in the config is kept.
        _enabled = cfg.Bind("Triple", "SpanSeparateMonitors", true, "With three side-by-side monitors and no Surround, open the game as one borderless window across all of them so the triple views can run (STD-015). Set false to keep the stock single screen.");
    }

    /// <summary>The virtual-desktop span when it should be used, else null.</summary>
    internal static (int X, int Y, int W, int H)? Target()
    {
        if (!_enabled.Value || !_triplesAllowed()) return null;
        int monitors = GetSystemMetrics(80); // SM_CMONITORS
        int x = GetSystemMetrics(76), y = GetSystemMetrics(77), w = GetSystemMetrics(78), h = GetSystemMetrics(79);
        int mainW = GetSystemMetrics(0), mainH = GetSystemMetrics(1); // primary monitor
        if (monitors < 3 || h != mainH || w < h * 2.9f) return null;   // not three equal-height monitors in a row
        if (mainW >= mainH * 2.9f) return null;                         // Surround: one wide display already
        return (x, y, w, h);
    }

    internal static void BeginApply() => _depth++;
    internal static void EndApply() { if (_depth > 0) _depth--; }

    /// <summary>Inside the game's startup apply: substitute the span, windowed. Returns true when changed.</summary>
    internal static bool Substitute(ref int width, ref int height, ref bool fullscreen)
    {
        if (_depth == 0) return false;
        var target = Target();
        if (target == null) return false;
        var (sx, sy, sw, sh) = target.Value;
        Runtime.Log.LogInfo($"Span window: {sw}x{sh} span over separate monitors at ({sx},{sy}); windowed instead of {width}x{height} fullscreen {fullscreen}");
        width = sw; height = sh; fullscreen = false;
        _attempts = 6; _next = Time.unscaledTime + 0.5f; _maintain = false; Status = "placing";
        return true;
    }

    /// <summary>Per frame from Runtime.Update.</summary>
    internal static void Tick()
    {
        if (_attempts <= 0) { Maintain(); return; }
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 1.5f; _attempts--;
        try
        {
            var target = Target();
            if (target == null) { _attempts = 0; Status = "not needed"; return; }
            var (x, y, w, h) = target.Value;
            var hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero) { Status = "waiting for the game window"; return; }
            Place(hwnd, x, y, w, h);
            bool done = Screen.width == w && Screen.height == h;
            Status = done ? $"spanning {w}x{h} at ({x},{y})" : $"placing ({Screen.width}x{Screen.height} so far)";
            Runtime.Log.LogInfo($"Span window: {Status}, attempt {6 - _attempts}");
            if (done && _attempts < 4) { _attempts = 0; _maintain = true; _nextCheck = Time.unscaledTime + 2f; }
        }
        catch (Exception ex) { _attempts = 0; Status = "failed: " + ex.Message; Runtime.Log.LogWarning("Span window " + Status); }
    }

    private static void Maintain()
    {
        if (!_maintain || Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 2f;
        try
        {
            var target = Target();
            if (target == null || Screen.fullScreen) { _maintain = false; return; }
            var (x, y, w, h) = target.Value;
            if (Math.Abs(Screen.width - w) > 100 || Math.Abs(Screen.height - h) > 100) { _maintain = false; Status = "left to the game (another size chosen)"; return; }
            var hwnd = FindGameWindow();
            if (hwnd == IntPtr.Zero) return;
            if (Place(hwnd, x, y, w, h)) Runtime.Log.LogInfo("Span window: restored the span");
        }
        catch (Exception ex) { _maintain = false; Runtime.Log.LogWarning("Span window maintain: " + ex.Message); }
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
    private static Exception? ScopeFinalizer(Exception? __exception) { EndApply(); return __exception; }
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
