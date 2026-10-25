using Dbce.Wheel.Display;

namespace WodenRallyEdge;

/// <summary>
/// Where the game window belongs when the mod is not spanning separate monitors: on the racing row's center, the primary
/// display (which is the whole row under Surround). Unity opens its window on the monitor saved in its own preferences.
/// On 2026-10-24 that preference still named the dashboard display from an earlier separate-monitor layout, so under
/// Surround the game opened at 1920x1080 on the dashboard. The decision is pure (fixed rectangles in fixtures).
/// </summary>
/// <remarks>
/// It never targets a display outside the row and never guesses. With no racing row (one display, or a layout the
/// toolkit policy refuses), or a window that is on no display (minimized), it leaves the window alone.
/// </remarks>
internal static class WindowPlacement
{
    /// <summary>The primary display's rectangle when the window sits on another display and a racing row exists, else null.</summary>
    internal static (int X, int Y, int W, int H)? Decide(IReadOnlyList<MonitorRect> monitors, (int X, int Y, int W, int H) window, out string reason)
    {
        var row = RacingRowPolicy.Select(monitors);
        if (row.Kind == RacingRowKind.None) { reason = "no racing row (" + row.Reason + "); the game keeps its window"; return null; }
        // Surround: the row is the primary alone. Separate: the primary is the row's center display.
        var home = row.Kind == RacingRowKind.Surround ? row.Displays[0] : row.Displays[1];
        int cx = window.X + window.W / 2, cy = window.Y + window.H / 2;
        var on = monitors.Where(m => cx >= m.X && cx < m.Right && cy >= m.Y && cy < m.Bottom).ToList();
        if (on.Count != 1) { reason = "the game window " + Text(window) + " is on no single display (minimized or moving); left as is"; return null; }
        if (on[0].Equals(home)) { reason = "the game window is on the racing row (" + home + ")"; return null; }
        reason = "the game window " + Text(window) + " is on " + on[0] + ", outside the racing row; moving it to " + home;
        return (home.X, home.Y, home.Width, home.Height);
    }

    internal static string Text((int X, int Y, int W, int H) r) => r.W + "x" + r.H + "@" + r.X + "," + r.Y;
}
