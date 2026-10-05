using UnityEngine;

namespace WodenRallyEdge;

// The same stripped-build primitives as Panel; no window/display/input changes.
internal static class StageStatusOverlay
{
    private static GUIStyle? _style;
    private static bool _failed;

    internal static void Draw(string status)
    {
        if (_failed) return;
        var color = GUI.color;
        bool enabled = GUI.enabled;
        try
        {
            float scale = Math.Clamp(Screen.height / 1080f, .75f, 2.5f);
            float width = Math.Min(920 * scale, Screen.width - 24 * scale);
            if (width <= 0) return;
            var rect = new Rect((Screen.width - width) / 2, 12 * scale, width, 92 * scale);
            _style ??= new GUIStyle();
            UiNative.Style(_style, (int)(18 * scale), true);
            _style.normal.textColor = Color.white;
            GUI.enabled = true;
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(.025f, .035f, .055f, .96f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12 * scale, rect.y + 8 * scale,
                rect.width - 24 * scale, rect.height - 16 * scale),
                new GUIContent(status + "\nWheel and motion output muted | F12 stops\nRestart the game for normal output"), _style);
        }
        catch (Exception ex)
        {
            _failed = true;
            Runtime.Log.LogWarning("Stage status overlay unavailable: " + ex.Message);
        }
        finally { GUI.color = color; GUI.enabled = enabled; }
    }
}
