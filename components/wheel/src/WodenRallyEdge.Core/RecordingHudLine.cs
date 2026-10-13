using Dbce.Wheel.Ui;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

// A read-only adapter over TelemetryOutput's actual recorder state. Session
// wording/layout remains in the vendored toolkit presentation.
public static class RecordingHudLine
{
    public static SessionStatusLine? Describe(TelemetryOutput output, bool outputsMuted) =>
        Describe(output.RecordingStatus, output.RecordingElapsedSeconds, output.RecordingCounters,
            output.RecordingError, outputsMuted, output.RecordingRequiresFinalization);

    public static SessionStatusLine? Describe(string status, double elapsedSeconds, RecordingCounters? counters,
        string? error, bool outputsMuted, bool requiresFinalization)
    {
        if (status == "Disabled") return null;
        long droppedSamples = counters?.DroppedSamples ?? 0;
        long droppedMarkers = counters?.DroppedMarkers ?? 0;
        long invalid = counters?.InvalidCount ?? 0;
        bool rejected = counters == null || droppedSamples != 0 || droppedMarkers != 0 || invalid != 0 ||
            counters?.ErrorCount > 0 || counters?.LimitCount > 0 ||
            counters?.QueueFullCount > 0 || counters?.ContentionCount > 0;
        var state = error != null ? SessionDisplayState.Incomplete : status switch
        {
            "Recording" => SessionDisplayState.Recording,
            "Stopping" => SessionDisplayState.Saving,
            "Completed" => rejected ? SessionDisplayState.Incomplete :
                requiresFinalization ? SessionDisplayState.DataWritten : SessionDisplayState.Saved,
            "LimitReached" => SessionDisplayState.Limited,
            _ => SessionDisplayState.Incomplete
        };
        // Woden has an F6 Telemetry Stop button, not DRIVE's F11 shortcut.
        var line = SessionStatusPresentation.Describe(state, elapsedSeconds, outputsMuted,
            stopKey: "F6 > Telemetry > Stop recording",
            recordingStopHint: requiresFinalization ? "F6 > Telemetry > Stop recording" : null)!;
        if (!rejected) return line;
        string detail = droppedSamples + droppedMarkers > 0 ? "  |  " + (droppedSamples + droppedMarkers) + " dropped" : "";
        if (invalid > 0) detail += "  |  " + invalid + " invalid";
        return new SessionStatusLine(line.Text + detail, SessionDisplayTone.Problem);
    }
}
