using Dbce.Wheel.Ui.Unity;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// Developer recordings only (DevSession). The actual recorder owns its state and
// output; this adapter only presents it on the center view while driving.
internal static class RecordingStatusOverlay
{
    private static readonly StrippedSessionStatusHud Hud = new(UiNative.Style);
    private static bool _failed;

    internal static void Draw(DiagnosticRecordingControl? control)
    {
        if (_failed || Panel.Open || Event.current == null || Event.current.type != EventType.Repaint) return;
        var output = Runtime.Output;
        if (output == null) return;
        try
        {
            var line = RecordingHudLine.Describe(output, Runtime.DiagnosticNoForce || StagePlayback.OutputMuted,
                control?.Finalization, control?.LastError);
            if (line != null) Hud.Draw(line, Screen.width >= Screen.height * 2.9f,
                StagePlayback.OutputMuted ? 112 : 12);
        }
        catch (Exception ex)
        {
            _failed = true;
            Runtime.Log.LogWarning("Recording indicator could not draw: " + ex);
        }
    }
}
