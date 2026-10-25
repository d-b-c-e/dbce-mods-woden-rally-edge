using Dbce.Wheel.Display;

namespace WodenRallyEdge;

// SpanWindow's use of the vendored toolkit WindowLanding (no span): tonight's Surround rig with the dashboard above and
// a stale saved index, the separate-monitor rig, one display, a minimized window, and the Unity PlayerPrefs value name.
// The toolkit's own vectors (shared/display/window-landing-vectors.txt) cover the policy fully. No Windows call,
// registry, game or display change.
internal static class WindowPlacementChecks
{
    static MonitorRect M(int x, int y, int w, int h, bool primary = false) => new(x, y, w, h, primary);
    static List<EngineDisplay> Engine(params MonitorRect[] order) => order.Select(m => new EngineDisplay(m.Width, m.Height)).ToList();
    // As SpanWindow calls it: never spanning, the window's rectangle now, the display it opened on.
    static WindowLanding Land(MonitorRect[] monitors, MonitorRect window, MonitorRect? opened, List<EngineDisplay>? engine, int? saved) =>
        WindowLandingPolicy.Decide(monitors, RacingRowPolicy.Select(monitors), false, window.X, window.Y, window.Width, window.Height, opened, engine, saved);

    // Unity's Windows PlayerPrefs value name: the key, "_h", and its 32-bit djb2-xor hash.
    static string PrefName(string key)
    {
        uint hash = 5381;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(key)) hash = hash * 33 ^ b;
        return key + "_h" + hash;
    }

    public static void Run(Action<bool, string> check)
    {
        var surround = M(0, 0, 7680, 1440, true);
        var dash = M(2886, -1080, 1920, 1080);
        var rig = new[] { surround, dash };

        // 2026-10-24: Unity's saved index 1 opened the game on the dashboard at 1920x1080 under Surround.
        var d = Land(rig, dash, dash, Engine(surround, dash), 1);
        check(d.Move && (d.X, d.Y, d.Width, d.Height) == (0, 0, 7680, 1440) && d.Reason.Contains("outside the racing row", StringComparison.Ordinal),
            "stale dashboard index under Surround: the game moves to the primary surface: " + d.Reason);
        check(d.Persist == 0, "and Unity's start monitor becomes the racing display, index 0: " + d.PersistReason);
        d = Land(rig, dash, dash, Engine(dash, surround), 0);
        check(d.Move && d.Persist == 1, "Unity listing the dashboard first saves index 1, not Windows' order: " + d.PersistReason);
        d = Land(rig, surround, surround, Engine(surround, dash), 0);
        check(!d.Move && d.Persist == null && d.PersistReason.Contains("already", StringComparison.Ordinal), "landed and saved: nothing to do: " + d.PersistReason);
        d = Land(rig, dash, dash, Engine(surround, dash), 0);
        check(d.Move && d.Persist == null && d.PersistReason.Contains("not proven", StringComparison.Ordinal),
            "a saved index that does not name the opened display is not rewritten: " + d.PersistReason);
        d = Land(rig, dash, dash, null, null);
        check(d.Move && d.Persist == null, "a move needs no engine list (SpanWindow's startup decision)");
        d = Land(rig, M(-32000, -32000, 160, 28), dash, null, null);
        check(!d.Move && d.Reason.Contains("no single display", StringComparison.Ordinal), "a minimized window is not moved: " + d.Reason);

        // Surround off: a side or dashboard window moves to the center display; three equal displays save nothing.
        MonitorRect left = M(-2560, 0, 2560, 1440), centre = M(0, 0, 2560, 1440, true), right = M(2560, 0, 2560, 1440), diag = M(314, -1080, 1920, 1080);
        var separate = new[] { diag, right, centre, left };
        d = Land(separate, diag, diag, Engine(left, centre, right, diag), 3);
        check(d.Move && (d.X, d.Y, d.Width, d.Height) == (0, 0, 2560, 1440) && d.Persist == null && d.PersistReason.Contains("ambiguous", StringComparison.Ordinal),
            "Surround off: dashboard to the center display, index ambiguous: " + d.PersistReason);
        check(Land(separate, left, left, null, null).Move && !Land(separate, centre, centre, null, null).Move, "a side display moves; the center stays");

        // One display, or one racing display with a dashboard: no row, nothing moves or is saved.
        var single = M(0, 0, 2560, 1440, true);
        d = Land(new[] { single }, single, single, Engine(single), 0);
        check(!d.Move && d.Persist == null && d.Reason.StartsWith("no racing row", StringComparison.Ordinal), "one monitor is a no-op: " + d.Reason);
        d = Land(new[] { single, dash }, dash, dash, Engine(single, dash), 1);
        check(!d.Move && d.Persist == null, "one racing display with a dashboard: no guess");

        // PlayerPrefs.SetInt("UnitySelectMonitor") writes the value Unity read tonight; the game's own PlayerPrefs
        // "Resolution" key is stored the same way (both are in the owner's registry).
        check(PrefName("UnitySelectMonitor") == "UnitySelectMonitor_h17969598", "the PlayerPrefs key is Unity's stored value name: " + PrefName("UnitySelectMonitor"));
        check(PrefName("Resolution") == "Resolution_h2981718891", "the game's own PlayerPrefs key maps the same way");
    }
}
