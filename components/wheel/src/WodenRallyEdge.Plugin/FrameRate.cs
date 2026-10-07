using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// The family frame-rate readout (STD-023/024, toolkit FrameRateMonitor): average, 1% low and worst frame over
/// ten seconds in F6 Cameras and the log, and an optional counter at the top right of the centre screen.
/// </summary>
internal static class FrameRate
{
    private static readonly Dbce.Wheel.Telemetry.FrameRateMonitor Monitor = new();
    private static double _nextLog;
    private static GUIStyle? _style;

    internal static string Summary => Monitor.Summary;

    /// <summary>Per frame with the unscaled frame time.</summary>
    internal static void Tick(double now, float frameSeconds)
    {
        Monitor.Tick(now, frameSeconds);
        if (Monitor.WindowCompleted && now >= _nextLog)
        { _nextLog = now + 30; Runtime.Log.LogInfo("Frame rate (10 s): " + Monitor.Summary + (TripleView.Active ? " (triples on)" : " (single view)")); }
    }

    internal static void Draw(bool enabled)
    {
        if (!enabled || Event.current == null || Event.current.type != EventType.Repaint) return;
        string text = Monitor.OverlayText;
        if (text.Length == 0) return;
        int size = Math.Max(14, Screen.height / 60);
        _style ??= new GUIStyle();
        UiNative.Style(_style, size, false);
        float right = TripleView.Active ? Screen.width * 2f / 3f : Screen.width;
        float width = size * 13f;   // left-aligned text placed against the right edge (alignment setters are stripped)
        var rect = new Rect(right - width - 12, 8, width, size * 2f);
        var old = GUI.color;
        try
        {
            _style.normal.textColor = Color.black; GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), new GUIContent(text), _style);
            _style.normal.textColor = Color.white; GUI.Label(rect, new GUIContent(text), _style);
        }
        finally { GUI.color = old; }
    }
}