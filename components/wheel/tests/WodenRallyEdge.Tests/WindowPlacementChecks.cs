using Dbce.Wheel.Display;

namespace WodenRallyEdge;

// SpanWindow's off-row decision and Unity monitor-index mapping (RacingRowPlacement, linked plugin source) with fixed
// rectangles: tonight's Surround rig with the dashboard display above, the separate-monitor rig, one display, and a
// minimized window. No Windows call, registry, game or display change.
internal static class WindowPlacementChecks
{
    static MonitorRect M(int x, int y, int w, int h, bool primary = false) => new(x, y, w, h, primary);
    static MonitorRect? Move(MonitorRect[] layout, MonitorRect window, out string reason) =>
        RacingRowPlacement.Move(layout, window.X, window.Y, window.Width, window.Height, out reason);

    // Unity's Windows PlayerPrefs value name: the key, "_h", and its 32-bit djb2-xor hash.
    static string PrefName(string key)
    {
        uint hash = 5381;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(key)) hash = hash * 33 ^ b;
        return key + "_h" + hash;
    }

    public static void Run(Action<bool, string> check)
    {
        // 2026-10-24: Surround primary plus the 1920x1080 dashboard display above. Unity's saved monitor index 1 opened
        // the game on the dashboard at 1920x1080.
        var surround = M(0, 0, 7680, 1440, true);
        var dash = M(2886, -1080, 1920, 1080);
        var rig = new[] { surround, dash };
        var moved = Move(rig, dash, out string reason);
        check(moved.Equals(surround) && reason.Contains("outside the racing row", StringComparison.Ordinal),
            "a saved dashboard index under Surround moves the game to the primary surface: " + moved + " " + reason);
        check(Move(rig, surround, out reason) == null && reason.Contains("on the racing row", StringComparison.Ordinal),
            "the game already fullscreen on the Surround primary is left alone: " + reason);
        check(Move(rig, M(2880, 180, 1920, 1080), out _) == null, "a smaller window on the primary is the player's choice");
        check(Move(new[] { dash, surround }, dash, out _).Equals(surround), "enumeration order does not matter");

        // Separate monitors (span off or triples off): the row's center display is home; sides and the dashboard are not.
        MonitorRect left = M(-2560, 0, 2560, 1440), centre = M(0, 0, 2560, 1440, true), right = M(2560, 0, 2560, 1440), diag = M(314, -1080, 1920, 1080);
        var separate = new[] { diag, right, centre, left };
        check(Move(separate, diag, out reason).Equals(centre), "separate rig: dashboard index moves to the center display: " + reason);
        check(Move(separate, left, out _).Equals(centre), "separate rig: a side display moves to the center display");
        check(Move(separate, centre, out _) == null, "separate rig: the center display is left alone");

        // Never guesses and never moves without a racing row.
        var single = M(0, 0, 2560, 1440, true);
        check(Move(new[] { single }, single, out reason) == null && reason.StartsWith("no racing row", StringComparison.Ordinal), "one monitor is a no-op: " + reason);
        check(Move(new[] { single }, M(100, 100, 1280, 720), out _) == null, "one monitor, windowed: no-op");
        check(Move(new[] { single, dash }, dash, out reason) == null && reason.StartsWith("no racing row", StringComparison.Ordinal),
            "a single display row with a dashboard refuses rather than guesses: " + reason);
        check(Move(rig, M(-32000, -32000, 160, 28), out reason) == null && reason.Contains("on no single display", StringComparison.Ordinal),
            "a minimized window is not moved: " + reason);

        // Across a grid of window centers on every display of both rigs, the only target is ever the primary display.
        foreach (var layout in new[] { rig, separate })
        {
            var primary = layout.Single(m => m.Primary);
            foreach (var m in layout)
                for (int i = 1; i < 4; i++)
                    for (int j = 1; j < 4; j++)
                    {
                        var target = RacingRowPlacement.Move(layout, m.X + m.Width * i / 4 - 320, m.Y + m.Height * j / 4 - 180, 640, 360, out _);
                        check(target == null || target.Equals(primary), "never targets a display other than the primary: " + target + " from " + m);
                        check((target == null) == m.Equals(primary), "moves exactly when the window is off the primary: " + m);
                    }
        }

        // Unity's start monitor. PlayerPrefs.SetInt("UnitySelectMonitor") is the registry value Unity read tonight, and
        // the game's own PlayerPrefs "Resolution" key is stored the same way (both seen in the owner's registry).
        check(PrefName("UnitySelectMonitor") == "UnitySelectMonitor_h17969598", "the PlayerPrefs key is Unity's stored value name: " + PrefName("UnitySelectMonitor"));
        check(PrefName("Resolution") == "Resolution_h2981718891", "the game's own PlayerPrefs key maps the same way");

        var home = RacingRowPlacement.Home(RacingRowPolicy.Select(rig))!.Value;
        (int, int)[] unityPrimaryFirst = { (7680, 1440), (1920, 1080) }, unityDashFirst = { (1920, 1080), (7680, 1440) };
        check(RacingRowPlacement.EngineIndex(rig, home, dash, unityPrimaryFirst, 1, out reason) == 0,
            "Surround on, dashboard saved as 1: the racing display is Unity index 0: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, dash, unityDashFirst, 0, out reason) == 1,
            "Unity's order is not Windows' order: the same rig listed dashboard first saves 1: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, dash, unityPrimaryFirst, null, out reason) == null && reason.Contains("not proven", StringComparison.Ordinal),
            "no saved key means index 0, which is not the dashboard the game opened on: refused: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, dash, unityDashFirst, null, out reason) == 1,
            "no saved key, dashboard listed first: saves 1: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, dash, unityPrimaryFirst, 0, out reason) == null && reason.Contains("not proven", StringComparison.Ordinal),
            "a saved index that does not name the display the game opened on proves no order: refused: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, dash, unityPrimaryFirst, 5, out reason) == null, "an out-of-range saved index is refused: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, surround, unityPrimaryFirst, 0, out reason) == null && reason.Contains("already", StringComparison.Ordinal),
            "already saved as the racing display: nothing written: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, dash, new[] { (7680, 1440) }, 0, out reason) == null && reason.Contains("not mapped", StringComparison.Ordinal),
            "a Unity list of another length is not mapped: " + reason);
        check(RacingRowPlacement.EngineIndex(rig, home, dash, new[] { (1920, 1080), (1920, 1080) }, 1, out reason) == null && reason.Contains("no engine display", StringComparison.Ordinal),
            "no Unity display of the racing size: refused: " + reason);

        // Surround off: three equal racing displays cannot be told apart by size, so nothing is saved.
        var centreHome = RacingRowPlacement.Home(RacingRowPolicy.Select(separate))!.Value;
        check(centreHome.Equals(centre), "separate rig: home is the center display");
        (int, int)[] unitySeparate = { (2560, 1440), (2560, 1440), (2560, 1440), (1920, 1080) };
        check(RacingRowPlacement.EngineIndex(separate, centreHome, diag, unitySeparate, 3, out reason) == null && reason.Contains("ambiguous", StringComparison.Ordinal),
            "Surround off: three equal racing displays are ambiguous: " + reason);
        check(RacingRowPlacement.Home(RacingRowPolicy.Select(new[] { single })) == null, "one monitor has no home: nothing is saved");
        check(RacingRowPlacement.Home(RacingRowPolicy.Select(new[] { single, dash })) == null, "one racing display with a dashboard has no row: nothing is saved");
    }
}
