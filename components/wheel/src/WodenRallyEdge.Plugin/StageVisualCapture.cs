using System.Text.Json;
using UnityEngine;

namespace WodenRallyEdge;

// Passive evidence for an already validated, supervised cold replay. No developer
// input mode, focus changes, display changes or native device access are required.
internal static class StageVisualCapture
{
    private static double _next = double.NaN;
    private static int _count;
    private static bool _failed;

    internal static void Tick(bool playing, int samples, double now)
    {
        string? directory = StageRunLifecycle.VisualDirectory;
        if (!playing || _failed || _count >= 5 || directory == null) return;
        if (double.IsNaN(_next)) { _next = now + 2; return; }
        if (now < _next) return;
        try
        {
            if (_count == 0)
            {
                if (Directory.Exists(directory)) throw new IOException("Visual evidence directory already exists");
                Directory.CreateDirectory(directory);
            }
            string stem = Path.Combine(directory, "frame-" + _count.ToString("00"));
            // Unity completes the screenshot at the end of this rendered frame.
            ScreenCapture.CaptureScreenshot(stem + ".png", 1);
            using var json = new FileStream(stem + ".json", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(json, new { wallSeconds = now, samples, width = Screen.width,
                height = Screen.height, mode = Screen.fullScreenMode.ToString(),
                triples = TripleView.Status, focused = Runtime.Focused });
            _count++;
            _next = now + 12;
        }
        catch (Exception ex)
        {
            _failed = true;
            Runtime.Log.LogWarning("Replay visual evidence unavailable: " + ex.Message);
        }
    }
}
