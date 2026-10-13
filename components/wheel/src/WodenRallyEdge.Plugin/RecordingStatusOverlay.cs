using Dbce.Wheel.Ui.Unity;
using UnityEngine;
using WodenRallyEdge.Core;

namespace WodenRallyEdge;

// The existing Lifecycle.OnGUI is visible while driving. The actual recorder
// owns its state and output; this adapter only presents it on the center view.
internal static class RecordingStatusOverlay
{
    private static readonly StrippedSessionStatusHud Hud = new(UiNative.Style);
    private static bool _failed;

    internal static void Draw()
    {
        if (_failed || Panel.Open || Event.current == null || Event.current.type != EventType.Repaint) return;
        var output = Runtime.Output;
        if (output == null) return;
        try
        {
            var line = RecordingHudLine.Describe(output, Runtime.DiagnosticNoForce || StagePlayback.OutputMuted);
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
