using System;
using UnityEngine;

namespace WodenRallyEdge;

/// <summary>
/// Camera/set name and key reminder near the bottom of the centre screen for
/// about three seconds. Developer comparisons and failed saves use the same
/// visible notice; the caller records the same text in the log.
/// </summary>
internal static class ComparisonNotice
{
    private const float Seconds = 3f;
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
        float width = text.Split('\n').Max(line => line.Length) * size * 0.55f;
        var rect = new Rect((Screen.width - width) / 2f, Screen.height - size * 4f, width, size * 2.5f);
        var old = GUI.color;
        try
        {
            _style.normal.textColor = Color.black; GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), new GUIContent(text), _style);
            _style.normal.textColor = Color.white; GUI.Label(rect, new GUIContent(text), _style);
        }
        finally { GUI.color = old; }
    }
}
