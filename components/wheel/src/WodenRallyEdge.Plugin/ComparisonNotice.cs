using System;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// The F7 comparison label (toolkit camera-framing-and-comparison policy): on each press the actual mode, step/count and,
/// only when the pick could not be saved, "Couldn't save view", shown for about 2.5 seconds centred on the centre screen (the middle of a span), white over a black shadow
/// so it reads on any scene. An on-screen notification, never a tooltip; the same text goes to the log by the caller.
/// </summary>
internal static class ComparisonNotice
{
    private const float Seconds = 2.5f;
    private static string? _text;
    private static float _until;
    private static GUIStyle? _style;

    internal static string? Text => _text != null && Time.unscaledTime <= _until ? _text : null;

    internal static void Show(string text) { _text = text; _until = Time.unscaledTime + Seconds; }

    internal static void Draw()
    {
        if (Event.current == null || Event.current.type != EventType.Repaint) return;
        var text = Text;
        if (text == null) return;
        int size = Math.Max(18, Screen.height / 40);
        _style ??= new GUIStyle();
        UiNative.Style(_style, size, false);
        float width = text.Length * size * 0.55f;   // left-aligned text centred by its rectangle (alignment setters are stripped)
        var rect = new Rect((Screen.width - width) / 2f, (Screen.height - size * 2f) / 2f, width, size * 2f);   // centre of the centre screen, clear of top-edge HUD and dev banners
        var old = GUI.color;
        try
        {
            _style.normal.textColor = Color.black; GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), new GUIContent(text), _style);
            _style.normal.textColor = Color.white; GUI.Label(rect, new GUIContent(text), _style);
        }
        finally { GUI.color = old; }
    }
}
