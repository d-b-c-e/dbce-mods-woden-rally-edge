using Dbce.Wheel.Display;

namespace WodenRallyEdge;

// SpanWindow's off-row decision (WindowPlacement, linked plugin source) with fixed rectangles: tonight's Surround rig
// with the dashboard display above, the separate-monitor rig, one display, and a minimized window. No Windows call,
// game or display change.
internal static class WindowPlacementChecks
{
    static MonitorRect M(int x, int y, int w, int h, bool primary = false) => new(x, y, w, h, primary);
    static (int, int, int, int) R(MonitorRect m) => (m.X, m.Y, m.Width, m.Height);

    public static void Run(Action<bool, string> check)
    {
        // 2026-10-24: Surround primary plus the 1920x1080 dashboard display above. Unity's saved monitor index 1 opened
        // the game on the dashboard at 1920x1080.
        var surround = M(0, 0, 7680, 1440, true);
        var dash = M(2886, -1080, 1920, 1080);
        var rig = new[] { surround, dash };
        var moved = WindowPlacement.Decide(rig, R(dash), out string reason);
        check(moved == (0, 0, 7680, 1440) && reason.Contains("outside the racing row", StringComparison.Ordinal),
            "a saved dashboard index under Surround moves the game to the primary surface: " + moved + " " + reason);
        check(WindowPlacement.Decide(rig, R(surround), out reason) == null && reason.Contains("on the racing row", StringComparison.Ordinal),
            "the game already fullscreen on the Surround primary is left alone: " + reason);
        check(WindowPlacement.Decide(rig, (2880, 180, 1920, 1080), out _) == null, "a smaller window on the primary is the player's choice");
        check(WindowPlacement.Decide(new[] { dash, surround }, R(dash), out _) == (0, 0, 7680, 1440), "enumeration order does not matter");

        // Separate monitors (span off or triples off): the row's center display is home; sides and the dashboard are not.
        MonitorRect left = M(-2560, 0, 2560, 1440), centre = M(0, 0, 2560, 1440, true), right = M(2560, 0, 2560, 1440), diag = M(314, -1080, 1920, 1080);
        var separate = new[] { diag, right, centre, left };
        check(WindowPlacement.Decide(separate, R(diag), out reason) == (0, 0, 2560, 1440), "separate rig: dashboard index moves to the center display: " + reason);
        check(WindowPlacement.Decide(separate, R(left), out _) == (0, 0, 2560, 1440), "separate rig: a side display moves to the center display");
        check(WindowPlacement.Decide(separate, R(centre), out _) == null, "separate rig: the center display is left alone");

        // Never guesses and never moves without a racing row.
        var single = M(0, 0, 2560, 1440, true);
        check(WindowPlacement.Decide(new[] { single }, R(single), out reason) == null && reason.StartsWith("no racing row", StringComparison.Ordinal),
            "one monitor is a no-op: " + reason);
        check(WindowPlacement.Decide(new[] { single }, (100, 100, 1280, 720), out _) == null, "one monitor, windowed: no-op");
        check(WindowPlacement.Decide(new[] { single, dash }, R(dash), out reason) == null && reason.StartsWith("no racing row", StringComparison.Ordinal),
            "a single display row with a dashboard refuses rather than guesses: " + reason);
        check(WindowPlacement.Decide(rig, (-32000, -32000, 160, 28), out reason) == null && reason.Contains("on no single display", StringComparison.Ordinal),
            "a minimized window is not moved: " + reason);

        // Across a grid of window centers on every display of both rigs, the only target is ever the primary display.
        foreach (var layout in new[] { rig, separate })
        {
            var primary = layout.Single(m => m.Primary);
            foreach (var m in layout)
                for (int i = 1; i < 4; i++)
                    for (int j = 1; j < 4; j++)
                    {
                        var target = WindowPlacement.Decide(layout, (m.X + m.Width * i / 4 - 320, m.Y + m.Height * j / 4 - 180, 640, 360), out _);
                        check(target == null || target == R(primary), "never targets a display other than the primary: " + target + " from " + m);
                        check((target == null) == m.Equals(primary), "moves exactly when the window is off the primary: " + m);
                    }
        }
    }
}
