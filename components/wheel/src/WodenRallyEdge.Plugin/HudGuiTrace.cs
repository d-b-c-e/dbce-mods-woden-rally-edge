#if WODEN_HUD_GUI_DIAGNOSTIC
using System.Diagnostics;
using UnityEngine;

namespace WodenRallyEdge;

// Private, compile-time-only observation of the menu HUD draw path. Buffering
// avoids file I/O during OnGUI; the normal plugin contains no trace calls.
internal static class HudGuiTrace
{
    private sealed record Row(string EnterUtc, long EnterStopwatchTicks, int Frame,
        string Event, string Recording, bool PanelOpen, bool OutputMuted,
        bool GateStopped, string ExitUtc, long ExitStopwatchTicks, string Phase);

    private static readonly object Sync = new();
    private static readonly List<Row> Rows = new();
    private static long _started;
    private const int MaxRows = 1024;
    private const double Seconds = 3.5;

    internal static int Begin()
    {
        var recording = Runtime.Output?.RecordingStatus ?? "unavailable";
        lock (Sync)
        {
            if (_started == 0)
            {
                if (recording != "Completed") return -1;
                _started = Stopwatch.GetTimestamp();
            }
            if (Rows.Count >= MaxRows || (Stopwatch.GetTimestamp() - _started) / (double)Stopwatch.Frequency > Seconds) return -1;
            var current = Event.current;
            Rows.Add(new(DateTime.UtcNow.ToString("O"), Stopwatch.GetTimestamp(), Time.frameCount,
                current == null ? "null" : current.type.ToString(), recording, Panel.Open,
                StagePlayback.OutputMuted, Runtime.Activity.Stopped, "", 0, "entered"));
            return Rows.Count - 1;
        }
    }

    internal static void End(int index, string phase)
    {
        if (index < 0) return;
        lock (Sync)
        {
            Rows[index] = Rows[index] with { ExitUtc = DateTime.UtcNow.ToString("O"),
                ExitStopwatchTicks = Stopwatch.GetTimestamp(), Phase = phase };
        }
    }

    internal static void Flush()
    {
        lock (Sync)
        {
            Runtime.Log.LogInfo($"HUD-GUI-TRACE v1 rows={Rows.Count} stopwatchFrequency={Stopwatch.Frequency} windowSeconds={Seconds}");
            foreach (var row in Rows)
                Runtime.Log.LogInfo($"HUD-GUI-TRACE enterUtc={row.EnterUtc} enterTicks={row.EnterStopwatchTicks} exitUtc={row.ExitUtc} exitTicks={row.ExitStopwatchTicks} frame={row.Frame} event={row.Event} recording={row.Recording} panel={row.PanelOpen} muted={row.OutputMuted} gateStopped={row.GateStopped} phase={row.Phase}");
        }
    }
}
#endif
