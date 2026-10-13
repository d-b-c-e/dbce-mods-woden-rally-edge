using Dbce.Wheel.Ui;
using Dbce.Wheel.Recording;

namespace WodenRallyEdge.Core;

// A read-only adapter over TelemetryOutput's actual recorder state. Session
// wording/layout remains in the vendored toolkit presentation.
public static class RecordingHudLine
{
    public static SessionStatusLine? Describe(TelemetryOutput output, bool outputsMuted, DiagnosticFinalization? finalization = null,
        string? controlError = null) =>
        Describe(output.RecordingStatus, output.RecordingElapsedSeconds, output.RecordingCounters,
            output.RecordingError, outputsMuted, output.RecordingRequiresFinalization, finalization, controlError);

    public static SessionStatusLine? Describe(string status, double elapsedSeconds, RecordingCounters? counters,
        string? error, bool outputsMuted, bool requiresFinalization, DiagnosticFinalization? finalization = null,
        string? controlError = null)
    {
        if (status == "Disabled") return null;
        long droppedSamples = counters?.DroppedSamples ?? 0;
        long droppedMarkers = counters?.DroppedMarkers ?? 0;
        long invalid = counters?.InvalidCount ?? 0;
        bool rejected = counters == null || droppedSamples != 0 || droppedMarkers != 0 || invalid != 0 ||
            counters?.ErrorCount > 0 || counters?.LimitCount > 0 ||
            counters?.QueueFullCount > 0 || counters?.ContentionCount > 0 || counters?.RejectedAfterStop > 0;
        var state = error != null ? SessionDisplayState.Incomplete : status switch
        {
            "Recording" => SessionDisplayState.Recording,
            "Stopping" => SessionDisplayState.Saving,
            "Completed" => rejected ? SessionDisplayState.Incomplete :
                requiresFinalization ? finalization?.State switch {
                    "saved" => SessionDisplayState.Saved,
                    "incomplete" => SessionDisplayState.Incomplete,
                    _ => SessionDisplayState.DataWritten
                } : SessionDisplayState.Saved,
            "LimitReached" => SessionDisplayState.Limited,
            _ => SessionDisplayState.Incomplete
        };
        // Woden has an F6 Telemetry Stop button, not DRIVE's F11 shortcut.
        var line = SessionStatusPresentation.Describe(state, elapsedSeconds, outputsMuted,
            stopKey: "F6 > Telemetry > Stop recording",
            recordingStopHint: requiresFinalization ? "F6 > Telemetry > Stop recording" : null)!;
        if (state == SessionDisplayState.DataWritten)
            line = new SessionStatusLine(line.Text + "  |  Keep recording companion open to finalize", line.Tone);
        if (state == SessionDisplayState.Incomplete && finalization?.Error is { Length: > 0 } detailError)
            line = new SessionStatusLine(line.Text + "  |  " + detailError, SessionDisplayTone.Problem);
        if (controlError is { Length: > 0 })
            line = new SessionStatusLine(line.Text + "  |  Stop control: " +
                controlError[..Math.Min(controlError.Length, 160)], SessionDisplayTone.Problem);
        if (!rejected) return line;
        string detail = droppedSamples + droppedMarkers > 0 ? "  |  " + (droppedSamples + droppedMarkers) + " dropped" : "";
        if (invalid > 0) detail += "  |  " + invalid + " invalid";
        return new SessionStatusLine(line.Text + detail, SessionDisplayTone.Problem);
    }
}
