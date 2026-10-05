using System.Globalization;
using UnityEngine;

namespace WodenRallyEdge;

// A supervised launch explicitly adds autoExit=true to its expiring request.
// Ordinary low-level stage commands do not quit the owner's game.
internal static class StageRunLifecycle
{
    private static bool _autoExit, _seenActive, _quit;
    private static double _exitAt = double.NaN;
    internal static string? ReplayPath { get; private set; }
    internal static void Initialize(string root)
    {
        _autoExit = _seenActive = _quit = false;
        _exitAt = double.NaN;
        ReplayPath = null;
        string path = Path.Combine(root, "request.txt");
        if (!File.Exists(path) || new FileInfo(path).Length > 8192) return;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(path))
        {
            int eq = line.IndexOf('=');
            if (eq <= 0 || !fields.TryAdd(line[..eq], line[(eq + 1)..])) return;
        }
        _autoExit = fields.TryGetValue("autoExit", out var exit) && exit == "true" &&
            fields.TryGetValue("id", out var id) && Guid.TryParseExact(id, "N", out _) &&
            fields.TryGetValue("expiresUtc", out var expiry) && DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var until) &&
            until > DateTimeOffset.UtcNow && until <= DateTimeOffset.UtcNow.AddMinutes(5) &&
            fields.TryGetValue("action", out var action) && (action == "record" || action == "replay");
        if (_autoExit && fields["action"] == "replay" && fields.TryGetValue("coldStart", out var startup) && startup == "native-arcade-v1" &&
            fields.TryGetValue("path", out var source)) ReplayPath = source;
    }
    internal static void Tick(bool active, string status, string? error, double now)
    {
        StageCaptureContext.ObserveScene();
        if (!_autoExit || _quit) return;
        _seenActive |= active;
        if (!active && (_seenActive || error != null))
        {
            if (double.IsNaN(_exitAt))
            {
                _exitAt = now + 8;
                Runtime.Log.LogInfo("Supervised stage finished: " + status + "; " + error + "; normal exit in 8 seconds.");
            }
            if (now >= _exitAt) { _quit = true; Application.Quit(); }
        }
    }
    internal static bool Closing => !double.IsNaN(_exitAt);
}
