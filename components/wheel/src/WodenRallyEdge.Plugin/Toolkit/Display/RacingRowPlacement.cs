// MIT. Portable source for the shared display library, beside RacingRow.cs (Woden's copy; proposed for shared/display).
// Where a non-spanning game window belongs on the racing row, and that display's index in the engine's own display
// list, for an engine that saves its start monitor (Unity's UnitySelectMonitor). Pure: no Windows, engine, registry,
// window or display-setting calls. Builds for netstandard2.0 and later.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dbce.Wheel.Display
{
    public static class RacingRowPlacement
    {
        /// <summary>The display a game window belongs on when it is not spanning: the primary, which is the whole row under
        /// Surround and the center of a separate-monitor row. Null with no racing row.</summary>
        public static MonitorRect? Home(RacingRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            return row.Kind == RacingRowKind.Surround ? row.Displays[0] : row.Kind == RacingRowKind.Separate ? row.Displays[1] : (MonitorRect?)null;
        }

        /// <summary>The one display holding the window's center, or null (minimized, moving, or between displays).</summary>
        public static MonitorRect? DisplayOf(IReadOnlyList<MonitorRect> monitors, int x, int y, int width, int height)
        {
            int cx = x + width / 2, cy = y + height / 2;
            var on = monitors.Where(m => cx >= m.X && cx < m.Right && cy >= m.Y && cy < m.Bottom).ToList();
            return on.Count == 1 ? on[0] : (MonitorRect?)null;
        }

        /// <summary>The home display when the window is on another display and a racing row exists, else null. It never
        /// targets a display outside the row and never guesses: no row, or a window on no single display, moves nothing.</summary>
        public static MonitorRect? Move(IReadOnlyList<MonitorRect> monitors, int x, int y, int width, int height, out string reason)
        {
            var row = RacingRowPolicy.Select(monitors);
            var home = Home(row);
            string window = width + "x" + height + "@" + x + "," + y;
            if (home == null) { reason = "no racing row (" + row.Reason + "); the game keeps its window"; return null; }
            var on = DisplayOf(monitors, x, y, width, height);
            if (on == null) { reason = "the game window " + window + " is on no single display (minimized or moving); left as is"; return null; }
            if (on.Value.Equals(home.Value)) { reason = "the game window is on the racing row (" + home + ")"; return null; }
            reason = "the game window " + window + " is on " + on + ", outside the racing row; moving it to " + home;
            return home;
        }

        /// <summary>
        /// The engine display index to save so the next launch opens on <paramref name="home"/>, or null when nothing should
        /// be written (already saved, or the mapping is not certain; <paramref name="reason"/> says which).
        /// </summary>
        /// <param name="monitors">The displays Windows reports.</param>
        /// <param name="start">The Windows display the game window opened on this launch, before any move.</param>
        /// <param name="engine">The engine's own display list, in its order (Unity: <c>Display.displays</c> system sizes).</param>
        /// <param name="stored">The saved start-monitor index, or null when none is saved (the engine then starts on 0).</param>
        /// <remarks>The engine order is not Windows' order and carries no positions, so a display is matched by size and only
        /// when exactly one engine display has the home size and the lists have the same length. The saved index must also
        /// name a display of the size the game actually opened on, proving the saved index and the list share one order.</remarks>
        public static int? EngineIndex(IReadOnlyList<MonitorRect> monitors, MonitorRect home, MonitorRect start,
            IReadOnlyList<(int Width, int Height)> engine, int? stored, out string reason)
        {
            string list = string.Join(", ", engine.Select((d, i) => i + ": " + d.Width + "x" + d.Height));
            if (engine.Count != monitors.Count) { reason = "the engine lists " + engine.Count + " displays (" + list + ") and Windows " + monitors.Count + "; not mapped"; return null; }
            var matches = Enumerable.Range(0, engine.Count).Where(i => engine[i].Width == home.Width && engine[i].Height == home.Height).ToList();
            if (matches.Count != 1)
            {
                reason = (matches.Count == 0 ? "no engine display is " : matches.Count + " engine displays are ") + home.Width + "x" + home.Height +
                    " (" + list + "); ambiguous, not saved";
                return null;
            }
            int current = stored ?? 0;
            if (current < 0 || current >= engine.Count || engine[current].Width != start.Width || engine[current].Height != start.Height)
            {
                reason = "the saved index " + (stored?.ToString() ?? "unset") + " does not name the " + start.Width + "x" + start.Height +
                    " display the game opened on (" + list + "); the engine's order is not proven, not saved";
                return null;
            }
            if (matches[0] == current) { reason = "the saved index " + current + " is already the racing display (" + list + ")"; return null; }
            reason = "engine display " + matches[0] + " is the racing display " + home + " (" + list + ")";
            return matches[0];
        }
    }
}
