using Dbce.Wheel.Ui;

namespace WodenRallyEdge.Core;

// A read-only adapter over TelemetryOutput's actual recorder state. Session
// wording/layout remains in the vendored toolkit presentation.
public static class RecordingHudLine
{
    public static SessionStatusLine? Describe(string status, double elapsedSeconds, long drops, string? error, bool outputsMuted)
    {
        if (status == "Disabled") return null;
        var state = error != null ? SessionDisplayState.Incomplete : status switch
        {
            "Recording" => SessionDisplayState.Recording,
            "Stopping" => SessionDisplayState.Saving,
            "Completed" => drops == 0 ? SessionDisplayState.Saved : SessionDisplayState.Incomplete,
            "LimitReached" => SessionDisplayState.Limited,
            _ => SessionDisplayState.Incomplete
        };
        // Woden has an F6 Telemetry Stop button, not DRIVE's F11 shortcut.
        var line = SessionStatusPresentation.Describe(state, elapsedSeconds, outputsMuted,
            stopKey: "F6 > Telemetry > Stop recording")!;
        return drops > 0 ? new SessionStatusLine(line.Text + "  |  " + drops + " dropped", SessionDisplayTone.Problem) : line;
    }
}
